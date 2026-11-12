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
        public Color trailColor = new Color(0.75f, 1f, 0.55f, 0.55f);

        [Header("Shot")]
        [Tooltip("Balls fired per move (the original fires one).")]
        public int ballsPerShot = 1;
        public float burstInterval = 0.08f;
        [Tooltip("Can the player fire again while a ball is still flying? (original: no)")]
        public bool allowFireWhileBallsFly;

        [Header("Feel")]
        public float breakShake = 0.25f;
        public float bigBreakShake = 0.5f;
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

        /// <summary>Called by <see cref="GameBootstrap"/> once every reference is set.</summary>
        public void Begin()
        {
            if (_started) return;
            _started = true;
            _font = hud != null ? hud.font : null;
            physics.Board = board;
            physics.OnContact = OnBallContact;
            if (hud != null)
            {
                hud.VariantForType = FirstVariantOfType;
                hud.Init(Restart, () => GoTo(LevelNumber - 1), () => GoTo(LevelNumber + 1));
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

            _goals.Clear();
            foreach (var g in level.goals) _goals[g.type] = g.count;
            foreach (var it in board.Items)
                if (_goals.ContainsKey(it.Data.type) && !it.IsBreakable)
                    Debug.LogWarning($"[GardenCannon] Goal type {it.Data.type} is mapped to {it.Kind} in the ItemCatalog; set it to Breakable.");

            MovesLeft = Mathf.Max(1, level.moves);
            CurrentState = State.Aiming;
            cannon?.SetAngle(0f);

            if (hud != null)
            {
                hud.SetLevel(number);
                hud.SetMoves(MovesLeft);
                hud.BuildGoals(level.goals, catalog);
                foreach (var g in level.goals) hud.SetGoal(g.type, g.count);
                _tutorial = number == 1;
                if (_tutorial) hud.ShowBanner(tutorialText); else hud.HideBanner();
            }
        }

        public void Restart() => GoTo(LevelNumber);

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
                    sfx?.Play(sfx.waterSplash, 0.6f);
                    fx?.Splash(new Vector3(b.Pos.x, board.WaterLine, 0f), board.CellSize);
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
            hud?.SetMoves(MovesLeft, true);
            if (_tutorial) { _tutorial = false; hud?.HideBanner(); }
            if (MovesLeft == 5) sfx?.Play(sfx.lastMoves, 0.8f, 0f);
            _burstLeft = Mathf.Max(1, ballsPerShot);
            _burstTimer = 0f;
        }

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
            sfx?.Play(sfx.ballFire, 0.8f);
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
            t.localScale = ballPrefab.transform.localScale * (ballVisualScale * board.CellSize);
            return t;
        }

        void AddTrail(GameObject ball)
        {
            if (ball.GetComponentInChildren<TrailRenderer>(true) != null) return;
            var tr = ball.AddComponent<TrailRenderer>();
            tr.time = 0.18f;
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            tr.widthMultiplier = physics.Radius * 2f;
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
            var e = item.Entry;
            Color c = ItemColor(item);
            bool broke = item.Hit();
            float cell = board.CellSize;
            if (broke)
            {
                sfx?.Play(e?.breakSfx != null ? e.breakSfx : sfx.ballCollide, 0.9f);
                fx?.Break(item.VisualCentre, c, cell * Mathf.Sqrt(Mathf.Max(1, item.Offsets.Count)));
                rig?.Shake(item.Offsets.Count > 1 ? bigBreakShake : breakShake);
                OnItemBroken(item);
            }
            else
            {
                sfx?.Play(e?.hitSfx != null ? e.hitSfx : sfx.ballCollide, 0.65f);
                fx?.Hit(new Vector3(ball.Pos.x, ball.Pos.y, 0f), item.IsBreakable ? c : new Color(0.9f, 0.9f, 0.85f), cell);
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
            var lines = new List<string>();
            foreach (var kv in _goals)
                if (kv.Value > 0) lines.Add($"{catalog.Get(kv.Key)?.label ?? ("Item " + kv.Key)}: {kv.Value}");
            hud?.ShowPopup("Out of Moves", new Color(0.79f, 0.15f, 0.12f),
                "Still to break:\n" + string.Join("\n", lines),
                ("Try Again", Restart, true));
        }
    }
}
