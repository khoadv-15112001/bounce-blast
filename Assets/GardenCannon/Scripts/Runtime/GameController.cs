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
        [HideInInspector] public BoosterBar boosters;
        [Header("Boosters")]
        [Tooltip("Damage per hit of the x2 fireballs.")]
        public int x2Damage = 2;
        [Tooltip("Rocket: damage dealt to every item in the 3x3 cells around the target.")]
        public int rocketDamage = 5;
        public float rocketFlightTime = 0.55f;
        public Color fireballColor = new Color(1f, 0.42f, 0.12f);
        [Tooltip("Bomb: damage to every item in the 5x5 cells around the target.")]
        public int bombDamage = 3;
        [Tooltip("Electro: damage to every block of the tapped block's colour.")]
        public int electroDamage = 3;
        [Tooltip("Glove: damage to every item in the tapped row.")]
        public int gloveDamage = 2;
        [Tooltip("Extra moves booster: moves added.")]
        public int extraMoves = 5;
        public Color kettlebellColor = new Color(0.55f, 0.6f, 0.7f);
        int _volleyDamage = 1;
        bool _volleyPierce;
        bool _boosterBanner;
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
            if (boosters != null)
            {
                boosters.Build();
                boosters.SelectionChanged = OnBoosterSelected;
                boosters.InstantUsed = OnInstantBooster;
            }
            GameHaptics.Init();
            sfx?.PlayMusic();
            if (hud != null)
            {
                hud.ButtonClicked = () =>
                {
                    sfx?.Play(sfx.buttonClick, 0.9f, 0f);
                    GameHaptics.Play(GameHapticMoment.UiButton);
                };
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
            sfx?.StopJingles();
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
            // One waiting ball per ball to shoot (moves x balls per shot).
            ammoBelt?.Fill(MovesLeft * Mathf.Max(1, ballsPerShot));
            boosters?.ResetForLevel();
            CurrentState = State.Aiming;
            cannon?.SetAngle(0f);
            cannon?.PlayRefill();
            sfx?.SetMusicDuck(false);
            sfx?.Play(sfx.refillLand, 0.6f, 0f);
            GameHaptics.Play(GameHapticMoment.LevelStart);

            if (hud != null)
            {
                hud.SetLevel(number);
                hud.SetMoves(MovesLeft);
                hud.BuildGoals(level.goals, catalog);
                foreach (var g in level.goals) hud.SetGoal(g.type, g.count);
                _tutorial = number == 1;
                if (_tutorial)
                {
                    hud.ShowBanner(tutorialText);
                    sfx?.Play(sfx.appear, 0.7f, 0f);
                    GameHaptics.Play(GameHapticMoment.TutorialShown);
                }
                else hud.HideBanner();
            }
        }

        public void Restart() => GoTo(LevelNumber);

        /// <summary>Settings button: music / sound / vibration toggles, replay, close (+ haptics tools in dev builds).</summary>
        public void OpenSettings()
        {
            if (hud == null) return;
            var items = new List<(string, System.Action, bool)>
            {
                ("Music: " + (GameAudio.MusicMuted ? "Off" : "On"), () => { sfx?.ToggleMusic(); OpenSettings(); }, false),
                ("Sound: " + (GameAudio.Muted ? "Off" : "On"), () => { sfx?.ToggleSound(); OpenSettings(); }, false),
                ("Vibration: " + (GameHaptics.VibrationOn ? "On" : "Off"), () => { GameHaptics.ToggleVibration(); OpenSettings(); }, false),
            };
            if (HapticsDebug.Available) items.Add(("Haptics Tools", OpenHapticsTools, false));
            items.Add(("Replay", () => { hud.ClosePopup(); Restart(); }, true));
            items.Add(("Close", () => hud.ClosePopup(), true));
            hud.ShowMenu("Settings", items.ToArray());
        }

        /// <summary>Dev builds only: the package Haptics Lab, the game's haptics map, and a reset of the device tuning.</summary>
        void OpenHapticsTools()
        {
            hud.ShowMenu("Haptics",
                ("Haptics Lab", () => { hud.ClosePopup(); HapticsDebug.OpenLab(); }, false),
                ("Game Haptics Map", () => { hud.ClosePopup(); HapticsDebug.OpenMap(); }, false),
                ("Reset device tuning", () => { HapticsDebug.ResetAll(); OpenHapticsTools(); }, false),
                ("Back", OpenSettings, true));
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
                    // Each ball fired pulls one waiting ball into the cannon.
                    ammoBelt?.Feed(MovesLeft * Mathf.Max(1, ballsPerShot) + _burstLeft);
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
                    GameHaptics.Play(GameHapticMoment.BallSplash);
                    var splashPos = new Vector3(b.Pos.x, board.SplashLine, 0f);
                    if (fx == null || !fx.PlayPrefab(fx.splashFx, splashPos, board.CellSize))
                        fx?.Splash(splashPos, board.CellSize);
                    ReleaseBall(b);
                    _balls.RemoveAt(i);
                    if (_balls.Count == 0 && _burstLeft == 0) GameHaptics.Play(GameHapticMoment.ShotEnded);
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
                if (HapticsDebug.BlocksPointer(Input.mousePosition)) return;
                if (hud != null && hud.HandleClick(Input.mousePosition)) return;
                if (boosters != null && CurrentState == State.Aiming)
                {
                    Vector2 g = rig != null ? rig.ScreenToGround(Input.mousePosition) : (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    if (boosters.HandleTap(g)) return;
                    if (boosters.SelectedUse == BoosterUse.Target)
                    {
                        if (board.IsOverBoard(g)) UseTargetBooster(g);
                        else boosters.Cancel();
                        return;
                    }
                }
                if (CurrentState == State.Aiming && MovesLeft > 0)
                {
                    _aiming = true;
                    GameHaptics.Play(GameHapticMoment.AimStart);
                }
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
                    GameHaptics.Play(GameHapticMoment.AimTick);
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
                GameHaptics.Play(GameHapticMoment.FireRefused);
                return;
            }
            MovesLeft--;
            hud?.SetMoves(MovesLeft, true);
            // x2 armed: this whole volley is made of fireballs dealing double damage.
            _volleyDamage = 1;
            _volleyPierce = false;
            if (boosters != null && boosters.SelectedUse == BoosterUse.NextShot)
            {
                var used = boosters.ConsumeSelected();
                if (used == BoosterId.X2) _volleyDamage = Mathf.Max(1, x2Damage);
                else if (used == BoosterId.Kettlebell) _volleyPierce = true;   // heavy balls go through items
            }
            if (_tutorial) { _tutorial = false; hud?.HideBanner(); }
            if (MovesLeft == 5)
            {
                sfx?.Play(sfx.lastMoves, 0.8f, 0f);
                GameHaptics.Play(GameHapticMoment.LastMoves);
            }
            else if (MovesLeft == 1) GameHaptics.Play(GameHapticMoment.LastMove);
            _burstLeft = Mathf.Max(1, ballsPerShot);
            _burstTimer = 0f;
            _firstOfVolley = true;
        }

        bool _firstOfVolley;

        void SpawnBall()
        {
            var b = new Ball { Pos = cannon.Muzzle, Vel = cannon.Direction * physics.Speed, Damage = _volleyDamage, Pierce = _volleyPierce };
            b.View = AcquireBallView();
            TintBall(b.View, b.Damage > 1 ? 1 : b.Pierce ? 2 : 0);
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
            GameHaptics.Play(_firstOfVolley ? GameHapticMoment.Fire : GameHapticMoment.VolleyBall);
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
            tr.widthMultiplier = TrailWidth;
            SetTrailColors(tr, false);
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
                GameHaptics.Play(GameHapticMoment.WallBounce);
                return;
            }
            if (item == null) return;
            ApplyHit(item, ball.Pos, ball, ball.Damage);
        }

        /// <summary>One hit on an item (a ball, or an explosion when <paramref name="ball"/> is null) with its feedback.</summary>
        void ApplyHit(BoardItem item, Vector2 point, Ball ball, int damage = 1)
        {
            var e = item.Entry;
            Color c = ItemColor(item);
            var result = item.HitAt(point);
            // Extra damage (x2 fireballs, rocket): more hits at once, feedback for the strongest outcome.
            for (int i = 1; i < damage && item.Alive && result != BoardItem.HitResult.Broken; i++)
            {
                var r = item.HitAt(point);
                if (r == BoardItem.HitResult.None || r == BoardItem.HitResult.Blocked) break;
                result = r;
            }
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
                GameHaptics.Play(GameHapticMoment.EggHatched);
            }
            if (result == BoardItem.HitResult.Opened)
            {
                sfx?.Play(e?.openSfx != null ? e.openSfx : e?.hitSfx, 0.85f);
                if (fx == null || !fx.PlayPrefabSlow(e?.openFx, item.VisualCentre, cell, lidFxLifetime))
                    fx?.Hit(item.VisualCentre, ItemColor(item), cell * 1.6f);
                GameHaptics.Play(GameHapticMoment.ObstacleStep);
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
                if (item.Offsets.Count > 1) GameHaptics.Play(GameHapticMoment.BigItemBroken);
                else GameHaptics.Play(GameHapticMoment.ItemBroken, combo);
                if (ball != null && (combo + 1) % 5 == 0) GameHaptics.Play(GameHapticMoment.ComboMilestone);
                OnItemBroken(item);
            }
            else
            {
                sfx?.Play(e?.hitSfx != null ? e.hitSfx : sfx.ballCollide, 0.65f);
                GameHaptics.Play(item.IsBreakable ? GameHapticMoment.ItemDamaged : GameHapticMoment.ItemHit);
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
                    GameHaptics.Play(GameHapticMoment.ItemBlocked);
                    fx?.Hit(new Vector3(point.x, point.y, 0f), new Color(0.9f, 0.9f, 0.85f), cell * 0.8f);
                    return true;
                case BoardItem.HitResult.Activated:
                    // Electric orb switched on.
                    sfx?.Play(e?.hitSfx, 0.8f);
                    fx?.Hit(item.LastPartPos, OrbBlue, cell * 1.2f);
                    fx?.Sparkle(item.LastPartPos, 5, cell * 0.6f, 1.2f);
                    GameHaptics.Play(GameHapticMoment.OrbActivated);
                    return true;
                case BoardItem.HitResult.PartBroken:
                    // A crate of the wooden box / a log fed into the cutter.
                    sfx?.Play(e?.partBreakSfx != null ? e.partBreakSfx : e?.hitSfx, 0.8f);
                    if (fx == null || !fx.PlayPrefab(e?.partFx, item.LastPartPos, cell))
                        fx?.Break(item.LastPartPos, item.Behaviour == ItemBehaviour.LogCutter ? WoodChips : c, cell, 1f);
                    rig?.Shake(breakShake * 0.6f);
                    GameHaptics.Play(GameHapticMoment.PartBroken);
                    return true;
                case BoardItem.HitResult.Fused:
                    // Brick wall: the dynamite fuse is lit.
                    sfx?.Play(e?.hitSfx, 0.8f);
                    fx?.Sparkle(item.VisualCentre, 6, cell * 0.5f, 1.4f);
                    GameHaptics.Play(GameHapticMoment.FuseLit);
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
                    GameHaptics.Play(GameHapticMoment.Explosion);
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
                    GameHaptics.Play(GameHapticMoment.OrbsReset);
                    item.PartPositions(_partPos);
                    foreach (var p in _partPos) fx?.Hit(p, new Color(0.4f, 0.45f, 0.6f), cell * 0.7f);
                    break;
            }
        }

        /// <summary>All orbs flash when the electric orb item discharges.</summary>
        void DischargeOrbs(BoardItem item)
        {
            GameHaptics.Play(GameHapticMoment.OrbsDischarged);
            item.PartPositions(_partPos);
            foreach (var p in _partPos)
            {
                fx?.Hit(p, OrbBlue, board.CellSize * 1.5f);
                fx?.Sparkle(p, 4, board.CellSize * 0.7f, 1.4f);
            }
        }

        // ---------------- Boosters ----------------

        static MaterialPropertyBlock _ballBlock;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Ball look: 0 normal, 1 x2 fireball (orange-red, fiery trail), 2 kettlebell (steel grey, heavy).</summary>
        void TintBall(Transform view, int kind)
        {
            bool fire = kind == 1;
            if (view == null) return;
            if (_ballBlock == null) _ballBlock = new MaterialPropertyBlock();
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                if (r is TrailRenderer tr)
                {
                    // Width stays relative to the ball (setting 1 here made the wide fan-shaped trails).
                    SetTrailColors(tr, fire);
                    tr.widthMultiplier = TrailWidth * (fire ? 1.3f : 1f);
                    continue;
                }
                if (kind != 0)
                {
                    r.GetPropertyBlock(_ballBlock);
                    _ballBlock.SetColor(ColorId, fire ? fireballColor : kettlebellColor);
                    r.SetPropertyBlock(_ballBlock);
                }
                else r.SetPropertyBlock(null);
            }
        }

        // Original trail (before the boosters): physics radius x 1.8 wide, trailColor fading to white.
        float TrailWidth => physics.Radius * 1.8f;

        void SetTrailColors(TrailRenderer tr, bool fire)
        {
            var g = new Gradient();
            if (fire)
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0f), new GradientColorKey(new Color(1f, 0.15f, 0f), 1f) },
                          new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            else
                g.SetKeys(new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
        }

        static string HintOf(BoosterId id) => id switch
        {
            BoosterId.Rocket => "Tap the board to\nlaunch the rocket!",
            BoosterId.Hammer => "Tap an item to\nsmash it!",
            BoosterId.Bomb => "Tap the board to\nthrow the bomb!",
            BoosterId.Electro => "Tap a block to zap\nall blocks of its colour!",
            BoosterId.Glove => "Tap a row to\npunch through it!",
            BoosterId.X2 => "Next shot: fireballs\ndeal double damage!",
            BoosterId.Kettlebell => "Next shot: heavy balls\ngo through the items!",
            _ => "",
        };

        void OnBoosterSelected(BoosterId? id)
        {
            if (id.HasValue)
            {
                sfx?.Play(sfx.appear, 0.8f, 0f);
                hud?.ShowBanner(HintOf(id.Value), true);
                _boosterBanner = true;
            }
            else if (_boosterBanner)
            {
                hud?.HideBanner();
                _boosterBanner = false;
            }
            GameHaptics.Play(GameHapticMoment.AimTick);
        }

        void OnInstantBooster(BoosterId id)
        {
            if (id != BoosterId.ExtraMoves) return;
            MovesLeft += Mathf.Max(1, extraMoves);
            hud?.SetMoves(MovesLeft, true);
            sfx?.Play(sfx.goalComplete != null ? sfx.goalComplete : sfx.appear, 0.8f, 0f);
            fx?.Sparkle(boosters.ButtonPos(id), 10, board.CellSize, 1.6f);
            GameHaptics.Play(GameHapticMoment.GoalComplete);
        }

        readonly List<BoardItem> _blast = new List<BoardItem>();

        void UseTargetBooster(Vector2 tap)
        {
            if (boosters == null) return;
            var id = boosters.ConsumeSelected();
            if (!id.HasValue) return;
            Vector2 target = board.CellCentreAt(tap);
            Vector3 from = boosters.ButtonPos(id.Value);
            Sprite icon = boosters.IconOf(id.Value);
            switch (id.Value)
            {
                case BoosterId.Rocket: StartCoroutine(Projectile(from, target, icon, true, () => Blast(target, 1, rocketDamage))); break;
                case BoosterId.Bomb: StartCoroutine(Projectile(from, target, icon, false, () => Blast(target, 2, bombDamage))); break;
                case BoosterId.Hammer: StartCoroutine(HammerSmash(target, icon)); break;
                case BoosterId.Electro: StartCoroutine(ElectroZap(from, target)); break;
                case BoosterId.Glove: StartCoroutine(GlovePunch(target, icon)); break;
            }
        }

        /// <summary>Sprite drawn over everything (3D items can't hide it).</summary>
        SpriteRenderer FlyingSprite(string name, Sprite icon, float size)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = icon;
            sr.sortingOrder = 90;
            if (DigitLabel.OverlayMaterial != null) sr.sharedMaterial = DigitLabel.OverlayMaterial;
            if (icon != null) go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, icon.bounds.size.x));
            return sr;
        }

        /// <summary>Rocket / bomb: arc from the button to the target cell, then <paramref name="onHit"/>.</summary>
        IEnumerator Projectile(Vector3 from, Vector2 target, Sprite icon, bool rocket, System.Action onHit)
        {
            int level = LevelNumber;
            sfx?.Play(sfx.ballFire, 0.9f, 0f);
            GameHaptics.Play(GameHapticMoment.Fire);
            var sr = FlyingSprite(rocket ? "Rocket" : "Bomb", icon, board.CellSize * 1.1f);
            var go = sr.gameObject;
            Vector3 to = new Vector3(target.x, target.y, -1.5f);
            float t = 0f, dur = Mathf.Max(0.15f, rocketFlightTime), smoke = 0f, spin = 0f;
            Vector3 prev = from;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / dur);
                float e = t * t * (3f - 2f * t);
                Vector3 p = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * (rocket ? 1.6f : 2.4f));
                go.transform.position = p;
                Vector3 d = p - prev;
                if (rocket)
                {
                    // The icon's nose points up-right: turn it along the flight.
                    if (d.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 45f);
                }
                else go.transform.rotation = Quaternion.Euler(0f, 0f, spin += Time.deltaTime * 360f);
                prev = p;
                smoke -= Time.deltaTime;
                if (smoke <= 0f) { fx?.Sparkle(p, 2, board.CellSize * 0.35f, 0.6f); smoke = 0.04f; }
                yield return null;
            }
            Destroy(go);
            if (level != LevelNumber || CurrentState == State.Lost) yield break;
            onHit?.Invoke();
        }

        /// <summary>Explosion: every item touching the (2r+1)x(2r+1) cells around the target takes <paramref name="damage"/>.</summary>
        void Blast(Vector2 target, int radiusCells, int damage)
        {
            float cell = board.CellSize;
            float half = cell * (radiusCells + 0.5f);
            board.ItemsInRect(new Rect(target.x - half, target.y - half, half * 2f, half * 2f), _blast);
            var hits = new List<BoardItem>(_blast);
            var boom = catalog != null ? catalog.Get(2)?.breakSfx : null;
            sfx?.PlayPitched(boom != null ? boom : sfx.ballCollide, 1f, 1f, 0.03f);
            fx?.Break(new Vector3(target.x, target.y, 0f), new Color(1f, 0.55f, 0.15f), cell, 2f + radiusCells);
            fx?.Sparkle(new Vector3(target.x, target.y, -1f), 10 + 6 * radiusCells, cell * (0.9f + 0.5f * radiusCells), 2.2f);
            rig?.Shake(bigBreakShake * (radiusCells > 1 ? 1.4f : 1f));
            GameHaptics.Play(GameHapticMoment.Explosion);
            foreach (var it in hits)
                if (it.Alive) ApplyHit(it, target, null, Mathf.Max(1, damage));
        }

        /// <summary>Hammer: drops on the tapped cell and smashes the item there (whatever its HP).</summary>
        IEnumerator HammerSmash(Vector2 target, Sprite icon)
        {
            int level = LevelNumber;
            float cell = board.CellSize;
            var sr = FlyingSprite("Hammer", icon, cell * 1.4f);
            var go = sr.gameObject;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.32f);
                // Raise, then swing down onto the cell.
                float swing = t < 0.45f ? Mathf.Lerp(0f, 35f, t / 0.45f) : Mathf.Lerp(35f, -55f, (t - 0.45f) / 0.55f);
                go.transform.position = new Vector3(target.x + cell * 0.45f, target.y + cell * 0.55f, -2f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, swing);
                yield return null;
            }
            if (level == LevelNumber && CurrentState != State.Lost)
            {
                board.ItemsInRect(new Rect(target.x - cell * 0.45f, target.y - cell * 0.45f, cell * 0.9f, cell * 0.9f), _blast);
                sfx?.PlayPitched(sfx.ballCollide, 1f, 0.8f, 0.02f);
                fx?.Sparkle(new Vector3(target.x, target.y, -1f), 10, cell, 1.8f);
                rig?.Shake(breakShake * 1.5f);
                GameHaptics.Play(GameHapticMoment.ItemBroken);
                foreach (var it in new List<BoardItem>(_blast))
                    if (it.Alive) ApplyHit(it, target, null, Mathf.Max(1, it.Hits));
            }
            yield return new WaitForSeconds(0.12f);
            Destroy(go);
        }

        /// <summary>Electro: lightning from the button to every block of the tapped block's colour.</summary>
        IEnumerator ElectroZap(Vector3 from, Vector2 target)
        {
            int level = LevelNumber;
            float cell = board.CellSize;
            board.ItemsInRect(new Rect(target.x - cell * 0.45f, target.y - cell * 0.45f, cell * 0.9f, cell * 0.9f), _blast);
            BoardItem picked = _blast.Count > 0 ? _blast[0] : null;
            var targets = new List<BoardItem>();
            if (picked != null && picked.Entry != null && picked.Entry.colorByVariant)
            {
                foreach (var it in board.Items)
                    if (it.Alive && !it.Shielded && it.Entry != null && it.Entry.colorByVariant && it.Data.variant == picked.Data.variant) targets.Add(it);
            }
            else if (picked != null) targets.Add(picked);
            var orb = catalog != null ? catalog.Get(6) : null;
            sfx?.Play(orb != null && orb.breakSfx != null ? orb.breakSfx : sfx.appear, 0.9f, 0f);
            GameHaptics.Play(GameHapticMoment.Explosion);
            var lines = new List<LineRenderer>();
            foreach (var it in targets)
            {
                var lr = new GameObject("Zap").AddComponent<LineRenderer>();
                lr.material = TrailMaterialFor();
                lr.startWidth = cell * 0.12f; lr.endWidth = cell * 0.05f;
                lr.startColor = new Color(0.6f, 0.95f, 1f, 1f); lr.endColor = new Color(1f, 1f, 1f, 0.9f);
                lr.sortingOrder = 91;
                lr.positionCount = 6;
                lines.Add(lr);
            }
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (targets[i] == null) continue;
                    Vector3 a = from + Vector3.back, b = targets[i].VisualCentre + Vector3.back * 1.5f;
                    for (int k = 0; k < 6; k++)
                    {
                        float f = k / 5f;
                        Vector3 p = Vector3.Lerp(a, b, f);
                        if (k > 0 && k < 5) p += (Vector3)(Random.insideUnitCircle * cell * 0.25f);
                        lines[i].SetPosition(k, p);
                    }
                }
                yield return null;
            }
            foreach (var lr in lines) Destroy(lr.gameObject);
            if (level != LevelNumber || CurrentState == State.Lost) yield break;
            foreach (var it in targets)
            {
                if (!it.Alive) continue;
                fx?.Sparkle(it.VisualCentre + Vector3.back, 4, cell * 0.7f, 1.4f);
                ApplyHit(it, it.VisualCentre, null, Mathf.Max(1, electroDamage));
            }
        }

        static Material _zapMaterial;
        static Material TrailMaterialFor()
        {
            if (_zapMaterial == null) _zapMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Zap" };
            return _zapMaterial;
        }

        /// <summary>Glove: sweeps along the tapped row from left to right, punching every item it passes.</summary>
        IEnumerator GlovePunch(Vector2 target, Sprite icon)
        {
            int level = LevelNumber;
            float cell = board.CellSize;
            board.ItemsInRect(new Rect(board.Left, target.y - cell * 0.45f, board.Right - board.Left, cell * 0.9f), _blast);
            var row = new List<BoardItem>(_blast);
            row.Sort((a, b) => a.Bounds.center.x.CompareTo(b.Bounds.center.x));
            var sr = FlyingSprite("Glove", icon, cell * 1.3f);
            var go = sr.gameObject;
            sfx?.Play(sfx.ballFire, 0.8f, 0f);
            float x = board.Left - cell, end = board.Right + cell;
            int next = 0;
            while (x < end)
            {
                x += Time.deltaTime * cell * 16f;
                go.transform.position = new Vector3(x, target.y, -2f);
                while (next < row.Count && row[next].Bounds.center.x <= x)
                {
                    var it = row[next++];
                    if (level != LevelNumber || CurrentState == State.Lost) continue;
                    if (!it.Alive) continue;
                    rig?.Shake(breakShake * 0.6f);
                    GameHaptics.Play(GameHapticMoment.ItemHit);
                    ApplyHit(it, new Vector2(it.Bounds.center.x, target.y), null, Mathf.Max(1, gloveDamage));
                }
                yield return null;
            }
            Destroy(go);
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
                if (left == 0)
                {
                    sfx?.Play(sfx.goalComplete, 0.8f, 0f);
                    GameHaptics.Play(GameHapticMoment.GoalComplete);
                }
                else
                {
                    sfx?.Play(sfx.goalHit, 0.35f, 0.03f);
                    GameHaptics.Play(GameHapticMoment.GoalHit);
                }
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
            sfx?.PlayJingle(sfx.levelWin, 1f);
            GameHaptics.Play(GameHapticMoment.LevelWon);
            StartCoroutine(WinSequence());
        }

        IEnumerator WinSequence()
        {
            int level = LevelNumber;
            yield return new WaitForSeconds(0.6f);
            if (CurrentState != State.Won || level != LevelNumber) yield break;
            sfx?.PlayJingle(sfx.levelWinFireworks, 0.8f);
            GameHaptics.Play(GameHapticMoment.WinFireworks);
            fx?.Confetti(new Vector3(board.Left + board.gridWidth * 0.5f, board.Top + 1f, 0f), board.gridWidth);
            yield return new WaitForSeconds(0.5f);
            if (CurrentState != State.Won || level != LevelNumber) yield break;
            sfx?.Play(sfx.appear, 0.7f, 0f);
            GameHaptics.Play(GameHapticMoment.PopupShown);
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
            sfx?.PlayJingle(sfx.outOfBalls, 0.9f);
            GameHaptics.Play(GameHapticMoment.LevelLost);
            StartCoroutine(LoseSequence());
        }

        IEnumerator LoseSequence()
        {
            int level = LevelNumber;
            yield return new WaitForSeconds(0.8f);
            if (CurrentState != State.Lost || level != LevelNumber) yield break;
            sfx?.PlayJingle(sfx.levelFail, 0.9f);
            GameHaptics.Play(GameHapticMoment.PopupShown);
            var lines = new List<string>();
            foreach (var kv in _goals)
                if (kv.Value > 0) lines.Add($"{catalog.Get(kv.Key)?.label ?? ("Item " + kv.Key)}: {kv.Value}");
            hud?.ShowPopup("Out of Moves", new Color(0.79f, 0.15f, 0.12f),
                "Still to break:\n" + string.Join("\n", lines),
                ("Try Again", Restart, true));
        }
    }
}
