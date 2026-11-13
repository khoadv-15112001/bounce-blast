using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Game loop (feel taken from the original game): drag to aim with a dotted bounce preview, release to fire
    /// ONE ball; it bounces around breaking items until it drops into the water, then the next shot is allowed.
    /// Win when every goal reaches 0, lose when moves run out.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public enum State { Aiming, Won, Lost }

        [Header("References (set by GameBootstrap)")]
        public CameraRig rig;
        public Board board;
        public CannonView cannon;
        public AimGuide aimGuide;
        public GameHud hud;
        public GameAudio sfx;
        public FxPool fx;
        public ItemCatalog catalog;
        public GameObject ballPrefab;
        [HideInInspector] public SceneArt3D sceneArt;
        [HideInInspector] public AmmoBelt ammoBelt;
        public SpriteRenderer lawnRenderer;
        public Sprite lawn9x12;
        public Sprite lawn12x17;

        [Header("Ball")]
        [Tooltip("Ball visual scale (multiplied by the cell size).")]
        public float ballVisualScale = 1f;
        public BallSimulator physics = new BallSimulator();
        [Tooltip("After this many seconds in flight the ball speeds up so a shot never drags on.")]
        public float speedUpAfter = 6f;
        public float maxSpeedScale = 1.8f;
        public Color trailColor = new Color(0.7f, 1f, 0.4f, 0.65f);

        [Header("Shot")]
        [Tooltip("Balls fired per move (updated game: a stream of 5).")]
        public int ballsPerShot = 5;
        [Tooltip("Seconds between the balls of one shot.")]
        public float burstInterval = 0.05f;
        [Tooltip("Can the player fire the next shot while balls of earlier shots are still flying?")]
        public bool allowFireWhileBallsFly = true;

        [Header("Feel")]
        public float breakShake = 0.25f;
        public float bigBreakShake = 0.5f;
        [Tooltip("Egg box lid VFX: plays this many times slower so the pieces fade out gently (1 = original speed).")]
        [Range(1f, 2f)] public float lidFxLifetime = 1.35f;
        public string tutorialText = "Drag to aim,\nrelease to shoot!";

        [Header("Start")]
        [Tooltip("0 = continue from saved progress.")]
        public int startLevel;

        public State CurrentState { get; private set; }
        public int LevelNumber { get; private set; }
        public int MovesLeft { get; private set; }

        readonly List<Ball> _balls = new List<Ball>();
        readonly Stack<Transform> _ballPool = new Stack<Transform>();
        readonly Dictionary<int, int> _goals = new Dictionary<int, int>();
        int _burstLeft;
        float _burstTimer;
        bool _aiming;
        float _lastAimTick;
        float _lastAngle;
        bool _started;
        bool _tutorial;
        Font _font;

        const string PrefKey = "gc_level";

        // One-time upgrade of values already saved in the scene when the defaults change.
        [SerializeField, HideInInspector] int _settingsVersion;
        const int SettingsVersion = 4;

        void OnValidate()
        {
            if (_settingsVersion >= SettingsVersion) return;
            if (_settingsVersion < 2)
            {
                ballsPerShot = 5;          // updated game fires a stream of 5 balls
                burstInterval = 0.05f;
                trailColor = new Color(0.7f, 1f, 0.4f, 0.65f);
            }
            if (_settingsVersion < 3) allowFireWhileBallsFly = true; // shoot again while balls are flying
            if (_settingsVersion < 4) lidFxLifetime = 1.35f;           // egg box lid: a bit slower, then fade
            _settingsVersion = SettingsVersion;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Called by <see cref="GameBootstrap"/> once every reference is set.</summary>
        public void Begin()
        {
            if (_started) return;
            _started = true;
            _font = hud != null ? hud.font : null;
            physics.Board = board;
            physics.OnContact = OnBallContact;
            board.ItemEvent = OnItemEvent;
            sfx?.PlayMusic();
            if (hud != null)
            {
                hud.ButtonClicked = () => sfx?.Play(sfx.buttonClick, 0.9f, 0f);
                hud.VariantForType = FirstVariantOfType;
                hud.Init(OpenSettings, () => GoTo(LevelNumber - 1), () => GoTo(LevelNumber + 1));
            }
            int start = startLevel > 0 ? startLevel : PlayerPrefs.GetInt(PrefKey, 1);
            GoTo(start);
        }

        // ---------------- Level flow ----------------

        public void GoTo(int number)
        {
            int count = Mathf.Max(1, LevelLibrary.Count);
            number = ((number - 1) % count + count) % count + 1;
            var level = LevelLibrary.Load(number);
            if (level == null) return;

            LevelNumber = number;
            PlayerPrefs.SetInt(PrefKey, number);
            PlayerPrefs.Save();

            ClearBalls();
            _burstLeft = 0;
            _aiming = false;
            aimGuide?.Hide();
            hud?.ClosePopup();

            if (lawnRenderer != null)
            {
                var s = level.width > 9 ? lawn12x17 : lawn9x12;
                if (s != null) lawnRenderer.sprite = s;
            }

            board.Build(level, catalog, _font);
            sceneArt?.RebuildLawn();

            _goals.Clear();
            foreach (var g in level.goals) _goals[g.type] = g.count;
            foreach (var it in board.Items)
                if (_goals.ContainsKey(it.Data.type) && !it.IsBreakable)
                    Debug.LogWarning($"[GardenCannon] Goal type {it.Data.type} is mapped to {it.Kind} in the ItemCatalog; set it to Breakable.");

            MovesLeft = Mathf.Max(1, level.moves);
            ammoBelt?.Fill(MovesLeft);
            CurrentState = State.Aiming;
            cannon?.SetAngle(0f);
            cannon?.PlayRefill();
            sfx?.SetMusicDuck(false);
            sfx?.Play(sfx.refillLand, 0.6f, 0f);

            if (hud != null)
            {
                hud.SetLevel(number);
                hud.SetMoves(MovesLeft);
                hud.BuildGoals(level.goals, catalog);
                foreach (var g in level.goals) hud.SetGoal(g.type, g.count);
                _tutorial = number == 1;
                if (_tutorial) { hud.ShowBanner(tutorialText); sfx?.Play(sfx.appear, 0.7f, 0f); }
                else hud.HideBanner();
            }
        }

        public void Restart() => GoTo(LevelNumber);

        /// <summary>Settings button: music / sound toggles, replay, close.</summary>
        public void OpenSettings()
        {
            if (hud == null) return;
            hud.ShowMenu("Settings",
                ("Music: " + (GameAudio.MusicMuted ? "Off" : "On"), () => { sfx?.ToggleMusic(); OpenSettings(); }, false),
                ("Sound: " + (GameAudio.Muted ? "Off" : "On"), () => { sfx?.ToggleSound(); OpenSettings(); }, false),
                ("Replay", () => { hud.ClosePopup(); Restart(); }, true),
                ("Close", () => hud.ClosePopup(), true));
        }

        int FirstVariantOfType(int type)
        {
            foreach (var it in board.Items)
                if (it.Data.type == type) return it.Data.variant;
            return 0;
        }

        // ---------------- Update ----------------

        void Update()
        {
            if (!_started) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            HandleInput();
            board.Tick(dt);

            if (_burstLeft > 0)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0f)
                {
                    SpawnBall();
                    _burstLeft--;
                    _burstTimer = burstInterval;
                }
            }

            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                var b = _balls[i];
                b.Age += dt;
                if (b.Age > speedUpAfter) b.SpeedScale = Mathf.Min(maxSpeedScale, b.SpeedScale + dt * 0.25f);
                physics.Advance(b, dt, false);
                if (b.View != null) b.View.position = BallPosition(b);
                if (b.Dead)
                {
                    sfx?.Play(sfx.waterSplash, 0.9f, 0.1f);
                    var splashPos = new Vector3(b.Pos.x, board.SplashLine, 0f);
                    if (fx == null || !fx.PlayPrefab(fx.splashFx, splashPos, board.CellSize))
                        fx?.Splash(splashPos, board.CellSize);
                    ReleaseBall(b);
                    _balls.RemoveAt(i);
                }
            }

            if (CurrentState == State.Aiming && MovesLeft <= 0 && _burstLeft == 0 && _balls.Count == 0)
                Lose();
        }

        Vector3 BallPosition(Ball b) => new Vector3(b.Pos.x, b.Pos.y, -physics.Radius);

        bool CanShoot => CurrentState == State.Aiming && MovesLeft > 0 && _burstLeft == 0 && (allowFireWhileBallsFly || _balls.Count == 0);

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) GoTo(LevelNumber - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) GoTo(LevelNumber + 1);

            if (Input.GetMouseButtonDown(0))
            {
                if (hud != null && hud.HandleClick(Input.mousePosition)) return;
                if (CurrentState == State.Aiming && MovesLeft > 0) _aiming = true;
            }
            if (CurrentState != State.Aiming) { _aiming = false; aimGuide?.Hide(); return; }

            if (Input.GetKey(KeyCode.LeftArrow)) cannon.SetAngle(cannon.Angle + 60f * Time.deltaTime);
            if (Input.GetKey(KeyCode.RightArrow)) cannon.SetAngle(cannon.Angle - 60f * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) Fire();

            if (_aiming)
            {
                Vector2 ground = rig != null ? rig.ScreenToGround(Input.mousePosition) : (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
                cannon.AimAt(ground);
                if (Mathf.Abs(cannon.Angle - _lastAngle) > 3f && Time.time - _lastAimTick > 0.05f)
                {
                    sfx?.Play(sfx.aimTick, 0.3f, 0.02f);
                    _lastAimTick = Time.time;
                    _lastAngle = cannon.Angle;
                }
                if (CanShoot) aimGuide?.Show(physics, cannon.Muzzle, cannon.Direction);
                else aimGuide?.Hide();
                if (Input.GetMouseButtonUp(0))
                {
                    _aiming = false;
                    aimGuide?.Hide();
                    Fire();
                }
            }
        }

        // ---------------- Shooting ----------------

        public void Fire()
        {
            if (CurrentState != State.Aiming) return;
            if (!CanShoot)
            {
                sfx?.Play(sfx.ballFireRefused, 0.6f);
                return;
            }
            MovesLeft--;
            ammoBelt?.Feed(MovesLeft);
            hud?.SetMoves(MovesLeft, true);
            if (_tutorial) { _tutorial = false; hud?.HideBanner(); }
            if (MovesLeft == 5) sfx?.Play(sfx.lastMoves, 0.8f, 0f);
            _burstLeft = Mathf.Max(1, ballsPerShot);
            _burstTimer = 0f;
            _firstOfVolley = true;
        }

        bool _firstOfVolley;

        void SpawnBall()
        {
            var b = new Ball { Pos = cannon.Muzzle, Vel = cannon.Direction * physics.Speed };
            b.View = AcquireBallView();
            if (b.View != null)
            {
                b.View.position = BallPosition(b);
                foreach (var tr in b.View.GetComponentsInChildren<TrailRenderer>(true)) tr.Clear();
            }
            _balls.Add(b);
            cannon.Kick();
            rig?.Shake(0.08f);
            // Cannon blast: full on the first ball of the volley, softer for the ones right behind it.
            sfx?.Play(sfx.ballFire, _firstOfVolley ? 0.9f : 0.4f);
            _firstOfVolley = false;
        }

        Transform AcquireBallView()
        {
            if (ballPrefab == null) return null;
            Transform t;
            if (_ballPool.Count > 0)
            {
                t = _ballPool.Pop();
                t.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(ballPrefab, transform);
                VisualUtil.StripRuntimeComponents(go);
                t = go.transform;
                AddTrail(go);
            }
            // Size the model so it matches the physics ball (works for the original and the new 3D ball).
            t.localScale = ballPrefab.transform.localScale * (ballVisualScale * 2f * physics.Radius / BallMeshSize());
            return t;
        }

        float _ballMeshSize = -1f;
        GameObject _ballMeshPrefab;

        /// <summary>Largest extent of the ball prefab's meshes at scale 1 (cached per prefab).</summary>
        float BallMeshSize()
        {
            if (_ballMeshPrefab == ballPrefab && _ballMeshSize > 0f) return _ballMeshSize;
            _ballMeshPrefab = ballPrefab;
            float size = 0f;
            var root = ballPrefab.transform;
            foreach (var mf in ballPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var s = mf.sharedMesh.bounds.size;
                var ls = mf.transform == root ? Vector3.one : mf.transform.lossyScale;
                size = Mathf.Max(size, s.x * ls.x, s.y * ls.y, s.z * ls.z);
            }
            _ballMeshSize = size > 1e-4f ? size : board.CellSize;
            return _ballMeshSize;
        }

        void AddTrail(GameObject ball)
        {
            if (ball.GetComponentInChildren<TrailRenderer>(true) != null) return;
            var tr = ball.AddComponent<TrailRenderer>();
            tr.time = 0.22f;
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            tr.widthMultiplier = physics.Radius * 1.8f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            tr.sortingOrder = 30;
        }

        void ReleaseBall(Ball b)
        {
            if (b.View == null) return;
            b.View.gameObject.SetActive(false);
            _ballPool.Push(b.View);
        }

        void ClearBalls()
        {
            foreach (var b in _balls) ReleaseBall(b);
            _balls.Clear();
        }

        // ---------------- Contacts ----------------

        void OnBallContact(Ball ball, BoardItem item, BallHit hit)
        {
            if (hit == BallHit.Wall)
            {
                sfx?.Play(sfx.wallBounce, 0.45f);
                return;
            }
            if (item == null) return;
            ApplyHit(item, ball.Pos, ball);
        }

        /// <summary>One hit on an item (a ball, or an explosion when <paramref name="ball"/> is null) with its feedback.</summary>
        void ApplyHit(BoardItem item, Vector2 point, Ball ball)
        {
            var e = item.Entry;
            Color c = ItemColor(item);
            var result = item.HitAt(point);
            bool broke = result == BoardItem.HitResult.Broken;
            float cell = board.CellSize;
            if (item.LastHitHatched)
            {
                // Egg hatches: shell pieces + a bird flies away.
                var eggPos = new Vector3(item.LastHatchPos.x, item.LastHatchPos.y, 0f);
                if (fx == null || !fx.PlayPrefab(e?.hatchFx, eggPos, cell))
                    fx?.Hit(item.LastHatchPos, new Color(1f, 0.97f, 0.9f), cell * 1.4f);
                fx?.Bird(item.LastHatchPos, cell, item.LastHatchMaterial);
                sfx?.Play(e?.hitSfx, 0.8f);
                sfx?.Play(e?.extraBreakSfx, 0.6f);
            }
            if (result == BoardItem.HitResult.Opened)
            {
                sfx?.Play(e?.openSfx != null ? e.openSfx : e?.hitSfx, 0.85f);
                if (fx == null || !fx.PlayPrefabSlow(e?.openFx, item.VisualCentre, cell, lidFxLifetime))
                    fx?.Hit(item.VisualCentre, ItemColor(item), cell * 1.6f);
                rig?.Shake(0.15f);
                return;
            }
            if (result == BoardItem.HitResult.Hatched) return;
            if (HandleObstacleResult(item, result, point, c)) return;
            if (broke)
            {
                // Consecutive breaks by the same ball climb in pitch (combo feel).
                int combo = ball != null ? ball.Combo++ : 0;
                float pitch = 1f + Mathf.Min(combo, 10) * 0.045f;
                sfx?.PlayPitched(e?.breakSfx != null ? e.breakSfx : sfx.ballCollide, 0.9f, pitch, 0.02f);
                if (e?.extraBreakSfx != null) sfx?.Play(e.extraBreakSfx, 0.7f);
                int tile = e != null && e.colorByVariant ? catalog.AtlasTileForVariant(item.Data.variant) : -1;
                var breakPos = item.Behaviour == ItemBehaviour.WoodenBox ? item.LastPartPos : item.VisualCentre;
                if (item.Behaviour == ItemBehaviour.ElectricOrb) DischargeOrbs(item);
                if (fx == null || !fx.PlayPrefab(e?.breakFx, breakPos, cell, catalog, tile))
                    fx?.Break(breakPos, c, cell, Mathf.Sqrt(Mathf.Max(1, item.Offsets.Count)));
                rig?.Shake(item.Offsets.Count > 1 ? bigBreakShake : breakShake);
                OnItemBroken(item);
            }
            else
            {
                sfx?.Play(e?.hitSfx != null ? e.hitSfx : sfx.ballCollide, 0.65f);
                if (fx == null || !fx.PlayPrefab(e?.hitFx, item.VisualCentre, cell))
                    fx?.Hit(new Vector3(point.x, point.y, 0f), item.IsBreakable ? c : new Color(0.9f, 0.9f, 0.85f), cell);
            }
        }

        // ---------------- Obstacles ----------------

        static readonly Color WoodChips = new Color(0.72f, 0.5f, 0.28f);
        static readonly Color OrbBlue = new Color(0.45f, 0.85f, 1f);
        readonly List<BoardItem> _near = new List<BoardItem>();
        readonly List<Vector3> _partPos = new List<Vector3>();

        /// <summary>Feedback for the obstacle-only hit results. Returns true when handled.</summary>
        bool HandleObstacleResult(BoardItem item, BoardItem.HitResult result, Vector2 point, Color c)
        {
            var e = item.Entry;
            float cell = board.CellSize;
            switch (result)
            {
                case BoardItem.HitResult.Blocked:
                    sfx?.Play(sfx.ballCollide, 0.45f);
                    fx?.Hit(new Vector3(point.x, point.y, 0f), new Color(0.9f, 0.9f, 0.85f), cell * 0.8f);
                    return true;
                case BoardItem.HitResult.Activated:
                    // Electric orb switched on.
                    sfx?.Play(e?.hitSfx, 0.8f);
                    fx?.Hit(item.LastPartPos, OrbBlue, cell * 1.2f);
                    fx?.Sparkle(item.LastPartPos, 5, cell * 0.6f, 1.2f);
                    return true;
                case BoardItem.HitResult.PartBroken:
                    // A crate of the wooden box / a log fed into the cutter.
                    sfx?.Play(e?.partBreakSfx != null ? e.partBreakSfx : e?.hitSfx, 0.8f);
                    if (fx == null || !fx.PlayPrefab(e?.partFx, item.LastPartPos, cell))
                        fx?.Break(item.LastPartPos, item.Behaviour == ItemBehaviour.LogCutter ? WoodChips : c, cell, 1f);
                    rig?.Shake(breakShake * 0.6f);
                    return true;
                case BoardItem.HitResult.Fused:
                    // Brick wall: the dynamite fuse is lit.
                    sfx?.Play(e?.hitSfx, 0.8f);
                    fx?.Sparkle(item.VisualCentre, 6, cell * 0.5f, 1.4f);
                    return true;
            }
            return false;
        }

        void OnItemEvent(BoardItem item, BoardItem.ItemEvent ev)
        {
            var e = item.Entry;
            float cell = board.CellSize;
            switch (ev)
            {
                case BoardItem.ItemEvent.Exploded:
                {
                    sfx?.PlayPitched(e?.breakSfx != null ? e.breakSfx : sfx.ballCollide, 1f, 1f, 0.05f);
                    if (fx == null || !fx.PlayPrefab(e?.breakFx, item.VisualCentre, cell))
                        fx?.Break(item.VisualCentre, new Color(0.55f, 0.35f, 0.25f), cell, 1.5f);
                    rig?.Shake(bigBreakShake);
                    // The blast hits everything around once (other walls go on cracking / exploding: chain reaction).
                    float radius = cell * Mathf.Max(0.5f, e != null ? e.blastRadius - 0.5f : 1f);
                    board.ItemsNear(item.VisualCentre, radius, _near, item);
                    var hits = new List<BoardItem>(_near);
                    foreach (var other in hits)
                        if (other.Alive && !other.IsFuseBurning) ApplyHit(other, item.VisualCentre, null);
                    board.RefreshAutotiles();
                    OnItemBroken(item);
                    break;
                }
                case BoardItem.ItemEvent.OrbsReset:
                    sfx?.Play(e?.altSfx, 0.8f);
                    item.PartPositions(_partPos);
                    foreach (var p in _partPos) fx?.Hit(p, new Color(0.4f, 0.45f, 0.6f), cell * 0.7f);
                    break;
            }
        }

        /// <summary>All orbs flash when the electric orb item discharges.</summary>
        void DischargeOrbs(BoardItem item)
        {
            item.PartPositions(_partPos);
            foreach (var p in _partPos)
            {
                fx?.Hit(p, OrbBlue, board.CellSize * 1.5f);
                fx?.Sparkle(p, 4, board.CellSize * 0.7f, 1.4f);
            }
        }

        Color ItemColor(BoardItem item)
        {
            var e = item.Entry;
            if (e != null && e.colorByVariant) return VisualUtil.AtlasTileColor(catalog.AtlasTileForVariant(item.Data.variant));
            return e != null ? e.debugColor : Color.white;
        }

        void OnItemBroken(BoardItem item)
        {
            int type = item.Data.type;
            if (_goals.TryGetValue(type, out int left) && left > 0)
            {
                left--;
                _goals[type] = left;
                hud?.SetGoal(type, left, true);
                if (left == 0) sfx?.Play(sfx.goalComplete, 0.8f, 0f);
                else sfx?.Play(sfx.goalHit, 0.35f, 0.03f);
            }
            if (CurrentState == State.Aiming && GoalsDone()) Win();
        }

        bool GoalsDone()
        {
            if (_goals.Count == 0)
            {
                foreach (var it in board.Items)
                    if (it.Alive && it.IsBreakable) return false;
                return true;
            }
            foreach (var kv in _goals)
                if (kv.Value > 0) return false;
            return true;
        }

        void Win()
        {
            CurrentState = State.Won;
            _burstLeft = 0;
            aimGuide?.Hide();
            hud?.HideBanner();
            rig?.Shake(0.4f);
            int next = LevelNumber + 1;
            PlayerPrefs.SetInt(PrefKey, next > LevelLibrary.Count ? 1 : next);
            PlayerPrefs.Save();
            sfx?.SetMusicDuck(true);
            sfx?.Play(sfx.levelWin, 1f, 0f);
            StartCoroutine(WinSequence());
        }

        IEnumerator WinSequence()
        {
            int level = LevelNumber;
            yield return new WaitForSeconds(0.6f);
            if (CurrentState != State.Won || level != LevelNumber) yield break;
            sfx?.Play(sfx.levelWinFireworks, 0.8f, 0f);
            fx?.Confetti(new Vector3(board.Left + board.gridWidth * 0.5f, board.Top + 1f, 0f), board.gridWidth);
            yield return new WaitForSeconds(0.5f);
            if (CurrentState != State.Won || level != LevelNumber) yield break;
            sfx?.Play(sfx.appear, 0.7f, 0f);
            hud?.ShowPopup("Well Done!", new Color(0.18f, 0.6f, 0.17f),
                $"Level {LevelNumber} complete\nMoves left: {MovesLeft}",
                ("Replay", Restart, false),
                ("Next", () => GoTo(LevelNumber + 1), true));
        }

        void Lose()
        {
            CurrentState = State.Lost;
            aimGuide?.Hide();
            hud?.HideBanner();
            sfx?.SetMusicDuck(true);
            sfx?.Play(sfx.outOfBalls, 0.9f, 0f);
            StartCoroutine(LoseSequence());
        }

        IEnumerator LoseSequence()
        {
            int level = LevelNumber;
            yield return new WaitForSeconds(0.8f);
            if (CurrentState != State.Lost || level != LevelNumber) yield break;
            sfx?.Play(sfx.levelFail, 0.9f, 0f);
            var lines = new List<string>();
            foreach (var kv in _goals)
                if (kv.Value > 0) lines.Add($"{catalog.Get(kv.Key)?.label ?? ("Item " + kv.Key)}: {kv.Value}");
            hud?.ShowPopup("Out of Moves", new Color(0.79f, 0.15f, 0.12f),
                "Still to break:\n" + string.Join("\n", lines),
                ("Try Again", Restart, true));
        }
    }
}
