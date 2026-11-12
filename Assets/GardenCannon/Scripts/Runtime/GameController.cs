using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Game loop: load level -> aim (drag) -> release to fire a burst -> balls bounce and break items ->
    /// win when every goal reaches 0, lose when moves run out and no ball is left.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public enum State { Aiming, Won, Lost }

        [Header("References")]
        public Camera cam;
        public Board board;
        public CannonView cannon;
        public AimGuide aimGuide;
        public GameHud hud;
        public GameAudio sfx;
        public ItemCatalog catalog;

        [Header("Ball")]
        public GameObject ballPrefab;
        [Tooltip("Ball visual scale (multiplied by the cell size).")]
        public float ballVisualScale = 1f;
        public BallSimulator physics = new BallSimulator();

        [Header("Shot")]
        [Tooltip("Balls fired per move.")]
        public int ballsPerShot = 6;
        public float burstInterval = 0.07f;
        [Tooltip("Can the player fire again while balls are still flying?")]
        public bool allowFireWhileBallsFly = true;

        [Header("Background swap for 12x17 levels")]
        public SpriteRenderer lawnRenderer;
        public Sprite lawn9x12;
        public Sprite lawn12x17;

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
        Font _font;

        const string PrefKey = "gc_level";

        void Start()
        {
            if (cam == null) cam = Camera.main;
            _font = hud != null ? hud.font : null;
            physics.Board = board;
            physics.OnContact = OnBallContact;
            if (hud != null)
            {
                hud.Init(cam, Restart, () => GoTo(LevelNumber - 1), () => GoTo(LevelNumber + 1));
                hud.VariantForType = FirstVariantOfType;
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
            // Every goal item must be breakable, whatever the catalog says.
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
                physics.Advance(b, dt, false);
                if (b.View != null) b.View.position = new Vector3(b.Pos.x, b.Pos.y, -0.5f);
                if (b.Dead)
                {
                    sfx?.Play(sfx.waterSplash, 0.5f);
                    ReleaseBall(b);
                    _balls.RemoveAt(i);
                }
            }

            if (CurrentState == State.Aiming && MovesLeft <= 0 && _burstLeft == 0 && _balls.Count == 0)
                Lose();
        }

        void HandleInput()
        {
            if (cam == null) return;
            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);

            if (Input.GetMouseButtonDown(0))
            {
                if (hud != null && hud.HandleClick(world)) return;
                if (CurrentState == State.Aiming && MovesLeft > 0) _aiming = true;
            }

            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) GoTo(LevelNumber - 1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) GoTo(LevelNumber + 1);
            if (CurrentState != State.Aiming) { _aiming = false; aimGuide?.Hide(); return; }
            if (Input.GetKey(KeyCode.LeftArrow)) cannon.SetAngle(cannon.Angle + 60f * Time.deltaTime);
            if (Input.GetKey(KeyCode.RightArrow)) cannon.SetAngle(cannon.Angle - 60f * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) Fire();

            if (_aiming)
            {
                cannon.AimAt(world);
                if (Mathf.Abs(cannon.Angle - _lastAngle) > 4f && Time.time - _lastAimTick > 0.06f)
                {
                    sfx?.Play(sfx.aimTick, 0.35f, 0.02f);
                    _lastAimTick = Time.time;
                    _lastAngle = cannon.Angle;
                }
                aimGuide?.Show(physics, cannon.Muzzle, cannon.Direction);
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
            bool busy = _burstLeft > 0 || (!allowFireWhileBallsFly && _balls.Count > 0);
            if (MovesLeft <= 0 || busy)
            {
                sfx?.Play(sfx.ballFireRefused, 0.6f);
                return;
            }
            MovesLeft--;
            hud?.SetMoves(MovesLeft);
            if (MovesLeft == 5) sfx?.Play(sfx.lastMoves, 0.8f, 0f);
            _burstLeft = Mathf.Max(1, ballsPerShot);
            _burstTimer = 0f;
        }

        void SpawnBall()
        {
            var b = new Ball { Pos = cannon.Muzzle, Vel = cannon.Direction * physics.Speed };
            b.View = AcquireBallView();
            if (b.View != null) b.View.position = new Vector3(b.Pos.x, b.Pos.y, -0.5f);
            _balls.Add(b);
            cannon.Kick();
            sfx?.Play(sfx.ballFire, 0.7f);
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
            }
            t.localScale = ballPrefab.transform.localScale * (ballVisualScale * board.CellSize);
            foreach (var tr in t.GetComponentsInChildren<TrailRenderer>(true)) tr.Clear();
            return t;
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
                sfx?.Play(sfx.wallBounce, 0.4f);
                return;
            }
            if (item == null) return;
            var e = item.Entry;
            bool broke = item.Hit();
            if (broke)
            {
                sfx?.Play(e?.breakSfx != null ? e.breakSfx : sfx.ballCollide, 0.9f);
                OnItemBroken(item);
            }
            else
            {
                sfx?.Play(e?.hitSfx != null ? e.hitSfx : sfx.ballCollide, 0.6f);
            }
        }

        void OnItemBroken(BoardItem item)
        {
            int type = item.Data.type;
            if (_goals.TryGetValue(type, out int left) && left > 0)
            {
                left--;
                _goals[type] = left;
                hud?.SetGoal(type, left);
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
            var lines = new List<string>();
            foreach (var kv in _goals)
                if (kv.Value > 0) lines.Add($"{catalog.Get(kv.Key)?.label ?? ("Item " + kv.Key)}: {kv.Value}");
            hud?.ShowPopup("Out of Moves", new Color(0.79f, 0.15f, 0.12f),
                "Still to break:\n" + string.Join("\n", lines),
                ("Try Again", Restart, true));
        }
    }
}
