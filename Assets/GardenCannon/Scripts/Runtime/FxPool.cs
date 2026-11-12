using UnityEngine;

namespace GardenCannon
{
    /// <summary>
    /// Lightweight hit / break / splash particles (chips with gravity + sparkles), created at runtime so no
    /// extra asset is needed. Uses the built-in Sprites/Default shader.
    /// </summary>
    public class FxPool : MonoBehaviour
    {
        public Sprite sparkleSprite;
        [Tooltip("Egg box bird: 2 flapping frames.")]
        public Sprite[] birdFrames;
        ParticleSystem _chips, _sparkles;

        // ---------------- Egg box birds (pooled sprites)

        class Flyer
        {
            public SpriteRenderer sr;
            public Vector3 start, vel;
            public float age, life, scale, flapRate;
        }

        readonly System.Collections.Generic.List<Flyer> _birdsActive = new System.Collections.Generic.List<Flyer>();
        readonly System.Collections.Generic.Stack<Flyer> _birdsFree = new System.Collections.Generic.Stack<Flyer>();

        /// <summary>A bird pops out of an egg, flaps and flies up off the board.</summary>
        public void Bird(Vector3 pos, float cellSize)
        {
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
            float dt = Time.deltaTime;
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
                // Hop out of the egg (quick pop), then fly up with a slight wobble, fading near the end.
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

        void Init()
        {
            if (_chips != null) return;
            _chips = Create("Chips", false);
            _sparkles = Create("Sparkles", true);
        }

        ParticleSystem Create(string name, bool sparkle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = sparkle ? 0f : 2.2f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, sparkle ? 0.3f : 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, sparkle ? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f) : AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (sparkle && sparkleSprite != null)
            {
                // Sparkle lives inside a 4096x4096 atlas: use the sprite's own UV rect (sprite sheet mode),
                // otherwise every particle shows the whole atlas.
                mat.mainTexture = sparkleSprite.texture;
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                sheet.AddSprite(sparkleSprite);
            }
            r.sharedMaterial = mat;
            r.sortingOrder = 80;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            return ps;
        }

        /// <summary>Block / box break: coloured chips flying up and falling, plus sparkles.</summary>
        public void Break(Vector3 pos, Color color, float scale = 1f)
        {
            Init();
            pos.z -= 1.2f * scale;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 14; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = pos + (Vector3)(dir * 0.25f * scale);
                p.velocity = new Vector3(dir.x * Random.Range(2f, 6f), dir.y * Random.Range(2f, 5f) + 4f, 0f) * scale;
                p.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.25f));
                p.startSize = Random.Range(0.14f, 0.32f) * scale;
                p.startLifetime = Random.Range(0.45f, 0.75f);
                p.rotation = Random.Range(0f, 360f);
                _chips.Emit(p, 1);
            }
            Sparkle(pos, 8, scale);
        }

        /// <summary>Small burst when a ball hits something without breaking it.</summary>
        public void Hit(Vector3 pos, Color color, float scale = 1f)
        {
            Init();
            pos.z -= 1.2f * scale;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                p.position = pos;
                p.velocity = (Vector3)(dir * Random.Range(1.5f, 3.5f)) * scale;
                p.startColor = color;
                p.startSize = Random.Range(0.08f, 0.16f) * scale;
                p.startLifetime = 0.3f;
                _chips.Emit(p, 1);
            }
            Sparkle(pos, 2, scale * 0.7f);
        }

        public void Sparkle(Vector3 pos, int count, float scale = 1f)
        {
            Init();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = pos + (Vector3)(Random.insideUnitCircle * 0.5f * scale);
                p.velocity = (Vector3)(Random.insideUnitCircle * 1.2f * scale);
                p.startColor = new Color(1f, 1f, 0.85f);
                p.startSize = Random.Range(0.25f, 0.5f) * scale;
                p.startLifetime = Random.Range(0.3f, 0.5f);
                _sparkles.Emit(p, 1);
            }
        }

        /// <summary>Ball dropping into the water.</summary>
        public void Splash(Vector3 pos, float scale = 1f)
        {
            Init();
            pos.z -= 0.5f;
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 10; i++)
            {
                p.position = pos;
                p.velocity = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(3f, 6f), 0f) * scale;
                p.startColor = new Color(0.75f, 0.93f, 1f, 0.95f);
                p.startSize = Random.Range(0.1f, 0.22f) * scale;
                p.startLifetime = Random.Range(0.35f, 0.55f);
                _chips.Emit(p, 1);
            }
        }
    }
}
