using System.Collections.Generic;
using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Pooled gameplay FX:
    ///  - chips (Art/FxAtlas FX_Chip, tinted with the item colour) when items are hit / break,
    ///  - sparkles (FX_Star / FX_Glow),
    ///  - confetti for the win (original ConfettiParticle01..05),
    ///  - water splash, egg box birds.
    /// Every particle system uses sprite-sheet mode, so particles only ever show their own sprite (never a whole atlas).
    /// </summary>
    public class FxPool : MonoBehaviour
    {
        [Header("Sprites (set from GameAssets)")]
        public Sprite chipSprite;
        public Sprite glowSprite;
        public Sprite starSprite;
        public Sprite[] confettiSprites;
        [Tooltip("Egg box bird: 2 flapping frames.")]
        public Sprite[] birdFrames;
        [Tooltip("Egg box: 3D balloon that floats away when an egg hatches (replaces the bird when set).")]
        public GameObject balloonPrefab;
        [Tooltip("Balloon colours (one is picked at random for the balloon body).")]
        public Material[] balloonMaterials;
        [Tooltip("Balloon size in cells.")]
        public float balloonScale = 1.3f;
        [Tooltip("Original ball splash effect (VFX_Ball_Splash).")]
        public GameObject splashFx;

        ParticleSystem _chips, _sparkles, _confetti;
        bool _ready;

        // ------------------------------------------------------------------ setup

        void Init()
        {
            if (_ready) return;
            _ready = true;
            _chips = Create("Chips", 2.2f, new[] { chipSprite }, 600);
            _sparkles = Create("Sparkles", 0f, new[] { starSprite, glowSprite }, 300);
            _confetti = Create("Confetti", 0.55f, confettiSprites, 400);
            ConfigureFade(_chips, 0.7f);
            ConfigureFade(_sparkles, 0.3f);
            ConfigureFade(_confetti, 0.85f);
        }

        ParticleSystem Create(string name, float gravity, Sprite[] sprites, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { name = name + "Mat" };
            r.sharedMaterial = mat;
            r.sortingOrder = 80;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;

            // Sprite-sheet mode: UVs come from the sprites, the material only needs their texture.
            int valid = 0;
            var sheet = ps.textureSheetAnimation;
            if (sprites != null)
            {
                foreach (var s in sprites)
                {
                    if (s == null) continue;
                    if (valid == 0)
                    {
                        mat.mainTexture = s.texture;
                        sheet.enabled = true;
                        sheet.mode = ParticleSystemAnimationMode.Sprites;
                    }
                    else if (s.texture != mat.mainTexture) continue; // all sprites must share one texture
                    sheet.AddSprite(s);
                    valid++;
                }
            }
            if (valid > 0)
            {
                // One random sprite per particle, no animation over the lifetime.
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, valid - 0.001f);
                sheet.cycleCount = 1;
            }
            return ps;
        }

        static void ConfigureFade(ParticleSystem ps, float holdUntil)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Item breaks: chips burst up and fall + sparkles. <paramref name="spread"/> grows with the item size
        /// (more chips over a bigger area, chip size stays per cell).</summary>
        public void Break(Vector3 pos, Color color, float cell, float spread = 1f)
        {
            Init();
            pos.z -= 1.2f * cell;
            spread = Mathf.Clamp(spread, 1f, 3f);
            int count = Mathf.RoundToInt(12 * spread);
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = pos + (Vector3)(Random.insideUnitCircle * 0.4f * cell * spread);
                p.velocity = new Vector3(dir.x * Random.Range(2f, 6f), dir.y * Random.Range(2f, 5f) + 4f, 0f) * cell;
                p.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.2f));
                p.startSize = Random.Range(0.22f, 0.42f) * cell;
                p.startLifetime = Random.Range(0.5f, 0.8f);
                p.rotation = Random.Range(0f, 360f);
                _chips.Emit(p, 1);
            }
            Sparkle(pos, Mathf.RoundToInt(6 * spread), cell, spread);
        }

        /// <summary>Small burst when a ball hits something without breaking it.</summary>
        public void Hit(Vector3 pos, Color color, float cell)
        {
            Init();
            pos.z -= 1.2f * cell;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = pos;
                p.velocity = (Vector3)(dir * Random.Range(1.5f, 3.5f)) * cell;
                p.startColor = color;
                p.startSize = Random.Range(0.12f, 0.2f) * cell;
                p.startLifetime = 0.3f;
                p.rotation = Random.Range(0f, 360f);
                _chips.Emit(p, 1);
            }
            Sparkle(pos, 2, cell * 0.7f, 1f);
        }

        public void Sparkle(Vector3 pos, int count, float cell, float spread = 1f)
        {
            Init();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos + (Vector3)(Random.insideUnitCircle * 0.5f * cell * spread);
                p.velocity = (Vector3)(Random.insideUnitCircle * 1.2f * cell);
                p.startColor = new Color(1f, 1f, 0.85f);
                p.startSize = Random.Range(0.35f, 0.65f) * cell;
                p.startLifetime = Random.Range(0.3f, 0.5f);
                p.rotation = Random.Range(0f, 360f);
                _sparkles.Emit(p, 1);
            }
        }

        /// <summary>Ball dropping into the water.</summary>
        public void Splash(Vector3 pos, float cell)
        {
            Init();
            pos.z -= 0.5f;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 10; i++)
            {
                p.position = pos;
                p.velocity = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(3f, 6f), 0f) * cell;
                p.startColor = new Color(0.75f, 0.93f, 1f, 0.95f);
                p.startSize = Random.Range(0.1f, 0.2f) * cell;
                p.startLifetime = Random.Range(0.35f, 0.55f);
                p.rotation = Random.Range(0f, 360f);
                _chips.Emit(p, 1);
            }
        }

        /// <summary>Win: confetti rains from the top of the board across <paramref name="width"/>.</summary>
        public void Confetti(Vector3 top, float width, int count = 90)
        {
            Init();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = top + new Vector3(Random.Range(-0.5f, 0.5f) * width, Random.Range(0f, 2.5f), -3f);
                p.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-2f, 3f), 0f);
                p.startColor = Color.white;
                p.startSize = Random.Range(0.45f, 0.8f);
                p.startLifetime = Random.Range(2.2f, 3.2f);
                p.rotation = Random.Range(0f, 360f);
                _confetti.Emit(p, 1);
            }
            Sparkle(top + Vector3.down * 4f, 14, 1.3f, 3f);
        }

        // ------------------------------------------------------------------ original VFX prefabs (pooled)

        class PrefabFx
        {
            public GameObject prefab, go;
            public ParticleSystem[] systems;
            public ParticleSystem.MinMaxGradient[] baseColors;
            public bool[] tintable;   // white-textured 3D shards that the original game coloured at runtime
            public bool tinted;
            public float[] baseSimSpeed;
            public float lifetimeScale = 1f;
            public float duration, endTime, startTime;
        }

        readonly Dictionary<GameObject, Stack<PrefabFx>> _prefabFree = new Dictionary<GameObject, Stack<PrefabFx>>();
        readonly List<PrefabFx> _prefabActive = new List<PrefabFx>();

        /// <summary>Play an original effect prefab (Assets/GardenCannon/VFX) at <paramref name="pos"/>; scale 1 = one cell.</summary>
        public bool PlayPrefab(GameObject prefab, Vector3 pos, float cell) => PlayPrefab(prefab, pos, cell, null, -1);

        /// <summary>Same, recolouring BlockColorAtlas mesh particles to <paramref name="atlasTile"/> (block shards).</summary>
        public bool PlayPrefab(GameObject prefab, Vector3 pos, float cell, ItemCatalog catalog, int atlasTile)
        {
            bool ok = PlayPrefabInternal(prefab, pos, cell, atlasTile >= 0 ? VisualUtil.AtlasTileColor(atlasTile) : (Color?)null, 1f);
            if (ok && catalog != null && atlasTile >= 0)
            {
                var fx = _prefabActive[_prefabActive.Count - 1];
                VisualUtil.ApplyAtlasTileToRenderers(fx.go.GetComponentsInChildren<ParticleSystemRenderer>(true), catalog, atlasTile);
            }
            return ok;
        }

        /// <summary>
        /// The block shards (Cube Transparent / box_uv_05) are white: the original BlockBlastAnim script tinted them with the
        /// block colour. Only those mesh particles are tinted; sparkles, rings and shadows keep their own colours.
        /// </summary>
        static bool IsTintable(ParticleSystem ps)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null || r.renderMode != ParticleSystemRenderMode.Mesh) return false;
            var mat = r.sharedMaterial;
            if (mat == null || !mat.HasProperty("_MainTex") || mat.mainTexture == null) return false;
            return mat.mainTexture.name.StartsWith("box_uv");
        }

        static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color c)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(g.color * c);
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(g.colorMin * c, g.colorMax * c);
                default: return new ParticleSystem.MinMaxGradient(c);
            }
        }

        /// <summary>
        /// Play the effect <paramref name="lifetimeScale"/> times slower: the pieces follow exactly the original path
        /// (so they don't fly further), they just take longer and their colour-over-lifetime fade-out lasts longer.
        /// </summary>
        public bool PlayPrefabSlow(GameObject prefab, Vector3 pos, float cell, float lifetimeScale)
            => PlayPrefabInternal(prefab, pos, cell, null, Mathf.Max(0.1f, lifetimeScale));

        void ApplyLifetimeScale(PrefabFx fx, float scale)
        {
            if (Mathf.Approximately(scale, fx.lifetimeScale)) return;
            for (int i = 0; i < fx.systems.Length; i++)
            {
                var main = fx.systems[i].main;
                main.simulationSpeed = fx.baseSimSpeed[i] / scale;
            }
            fx.lifetimeScale = scale;
        }

        bool PlayPrefabInternal(GameObject prefab, Vector3 pos, float cell, Color? tint, float lifetimeScale)
        {
            if (prefab == null) return false;
            if (!_prefabFree.TryGetValue(prefab, out var free)) _prefabFree[prefab] = free = new Stack<PrefabFx>();
            var fx = free.Count > 0 ? free.Pop() : null;
            if (fx == null)
            {
                var go = Instantiate(prefab, transform);
                fx = new PrefabFx { prefab = prefab, go = go, systems = go.GetComponentsInChildren<ParticleSystem>(true) };
                float d = 0.5f;
                foreach (var ps in fx.systems)
                {
                    var m = ps.main;
                    d = Mathf.Max(d, m.startDelay.constantMax + m.duration * 0.25f + m.startLifetime.constantMax);
                }
                fx.duration = Mathf.Min(d + 0.2f, 6f);
                fx.baseColors = new ParticleSystem.MinMaxGradient[fx.systems.Length];
                fx.tintable = new bool[fx.systems.Length];
                for (int i = 0; i < fx.systems.Length; i++)
                {
                    fx.baseColors[i] = fx.systems[i].main.startColor;
                    fx.tintable[i] = IsTintable(fx.systems[i]);
                }
                fx.baseSimSpeed = new float[fx.systems.Length];
                for (int i = 0; i < fx.systems.Length; i++) fx.baseSimSpeed[i] = fx.systems[i].main.simulationSpeed;
            }
            ApplyLifetimeScale(fx, lifetimeScale);
            if (tint.HasValue || fx.tinted)
            {
                for (int i = 0; i < fx.systems.Length; i++)
                {
                    if (!fx.tintable[i]) continue;
                    var m = fx.systems[i].main;
                    m.startColor = tint.HasValue ? Tint(fx.baseColors[i], tint.Value) : fx.baseColors[i];
                }
                fx.tinted = tint.HasValue;
            }
            var t = fx.go.transform;
            fx.go.SetActive(true);
            t.position = pos;
            t.rotation = prefab.transform.rotation;
            t.localScale = prefab.transform.localScale * cell;
            foreach (var ps in fx.systems)
            {
                ps.Clear(false);
                ps.Play(false);
            }
            // Safety cap only: the effect is normally released as soon as its last particle has faded out.
            fx.startTime = Time.time;
            fx.endTime = Time.time + fx.duration * lifetimeScale + 1f;
            _prefabActive.Add(fx);
            return true;
        }

        static bool AnyAlive(PrefabFx fx)
        {
            foreach (var ps in fx.systems)
                if (ps.IsAlive(false)) return true;
            return false;
        }

        void UpdatePrefabFx()
        {
            float now = Time.time;
            for (int i = _prefabActive.Count - 1; i >= 0; i--)
            {
                var fx = _prefabActive[i];
                if (now < fx.endTime && (now - fx.startTime < 0.25f || AnyAlive(fx))) continue;
                foreach (var ps in fx.systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                fx.go.SetActive(false);
                _prefabActive.RemoveAt(i);
                _prefabFree[fx.prefab].Push(fx);
            }
        }

        // ------------------------------------------------------------------ egg box balloons (pooled 3D)

        class Floater
        {
            public Transform t;
            public Renderer body;
            public Vector3 start;
            public float age, rise, accel, phase, sway, scale;
        }

        readonly List<Floater> _balloonsActive = new List<Floater>();
        readonly Stack<Floater> _balloonsFree = new Stack<Floater>();

        /// <summary>A balloon pops out of the egg, wobbles and floats up off the screen.</summary>
        public void Balloon(Vector3 pos, float cellSize, Material material = null)
        {
            var f = _balloonsFree.Count > 0 ? _balloonsFree.Pop() : null;
            if (f == null)
            {
                var go = Instantiate(balloonPrefab, transform);
                go.name = "Balloon";
                VisualUtil.StripRuntimeComponents(go);
                f = new Floater { t = go.transform, body = go.GetComponent<Renderer>() };
                if (f.body == null) f.body = go.GetComponentInChildren<Renderer>(true);
                // Flying balloons keep their string (only the ones in the egg box tray hide it).
            }
            f.t.gameObject.SetActive(true);
            if (f.body != null && material != null) f.body.sharedMaterial = material;
            else if (f.body != null && balloonMaterials != null && balloonMaterials.Length > 0)
            {
                var m = balloonMaterials[Random.Range(0, balloonMaterials.Length)];
                if (m != null) f.body.sharedMaterial = m;
            }
            // In front of the board (items stand up to ~1 cell towards the camera).
            f.start = new Vector3(pos.x, pos.y, -1.4f * cellSize);
            f.age = 0f;
            f.rise = Random.Range(1.4f, 1.9f) * cellSize;
            f.accel = Random.Range(2.6f, 3.4f) * cellSize;
            f.phase = Random.Range(0f, Mathf.PI * 2f);
            f.sway = Random.Range(0.18f, 0.3f) * cellSize;
            f.scale = balloonScale * cellSize;
            f.t.position = f.start;
            f.t.rotation = Quaternion.identity;
            f.t.localScale = Vector3.zero;
            _balloonsActive.Add(f);
        }

        void UpdateBalloons(float dt)
        {
            float top = Camera.main != null ? Camera.main.transform.position.y + 2f * Camera.main.orthographicSize + 6f : 30f;
            for (int i = _balloonsActive.Count - 1; i >= 0; i--)
            {
                var f = _balloonsActive[i];
                f.age += dt;
                float t = f.age;
                float y = f.start.y + f.rise * t + 0.5f * f.accel * t * t;
                if (y > top || t > 8f)
                {
                    f.t.gameObject.SetActive(false);
                    _balloonsActive.RemoveAt(i);
                    _balloonsFree.Push(f);
                    continue;
                }
                float x = f.start.x + Mathf.Sin(t * 2.4f + f.phase) * f.sway - Mathf.Sin(f.phase) * f.sway;
                f.t.position = new Vector3(x, y, f.start.z);
                // Pop out of the egg (overshoot), then a gentle wobble.
                float pop = t < 0.35f ? EaseOutBack(t / 0.35f) : 1f;
                f.t.localScale = Vector3.one * (f.scale * pop);
                f.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.4f + f.phase + 0.8f) * 9f);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        // ------------------------------------------------------------------ egg box birds (pooled sprites)

        class Flyer
        {
            public SpriteRenderer sr;
            public Vector3 start, vel;
            public float age, life, scale, flapRate;
        }

        readonly List<Flyer> _birdsActive = new List<Flyer>();
        readonly Stack<Flyer> _birdsFree = new Stack<Flyer>();

        /// <summary>A bird pops out of an egg, flaps and flies up off the board.</summary>
        public void Bird(Vector3 pos, float cellSize, Material balloonMaterial = null)
        {
            if (balloonPrefab != null) { Balloon(pos, cellSize, balloonMaterial); return; }
            if (birdFrames == null || birdFrames.Length == 0 || birdFrames[0] == null) return;
            var f = _birdsFree.Count > 0 ? _birdsFree.Pop() : null;
            if (f == null)
            {
                var go = new GameObject("Bird");
                go.transform.SetParent(transform, false);
                f = new Flyer { sr = go.AddComponent<SpriteRenderer>() };
                f.sr.sortingOrder = 85;
            }
            f.sr.gameObject.SetActive(true);
            float dir = Random.value < 0.5f ? -1f : 1f;
            f.start = new Vector3(pos.x, pos.y, -1.6f * cellSize);
            f.vel = new Vector3(dir * Random.Range(2.2f, 3.6f), Random.Range(7f, 9f), 0f) * cellSize;
            f.age = 0f;
            f.life = 1.3f;
            f.flapRate = Random.Range(11f, 14f);
            float w = birdFrames[0].bounds.size.x;
            f.scale = cellSize * 1.15f / Mathf.Max(0.01f, w);
            f.sr.flipX = dir < 0f;
            f.sr.color = Color.white;
            f.sr.sprite = birdFrames[0];
            f.sr.transform.position = f.start;
            f.sr.transform.localScale = Vector3.zero;
            _birdsActive.Add(f);
        }

        void Update()
        {
            UpdatePrefabFx();
            float dt = Time.deltaTime;
            UpdateBalloons(dt);
            for (int i = _birdsActive.Count - 1; i >= 0; i--)
            {
                var f = _birdsActive[i];
                f.age += dt;
                float t = f.age;
                if (t >= f.life)
                {
                    f.sr.gameObject.SetActive(false);
                    _birdsActive.RemoveAt(i);
                    _birdsFree.Push(f);
                    continue;
                }
                float pop = Mathf.Clamp01(t / 0.15f);
                float s = f.scale * (pop < 1f ? Mathf.Lerp(0.2f, 1.15f, pop) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f)));
                f.sr.transform.localScale = new Vector3(s, s, 1f);
                float climb = Mathf.Max(0f, t - 0.1f);
                f.sr.transform.position = f.start + new Vector3(f.vel.x * climb, f.vel.y * climb + Mathf.Sin(t * 9f) * 0.08f, 0f);
                int frame = (int)(t * f.flapRate) % Mathf.Min(2, birdFrames.Length);
                f.sr.sprite = birdFrames[frame];
                float fade = Mathf.Clamp01((f.life - t) / 0.35f);
                f.sr.color = new Color(1f, 1f, 1f, fade);
            }
        }
    }
}
