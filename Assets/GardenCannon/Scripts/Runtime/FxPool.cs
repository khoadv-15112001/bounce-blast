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
        ParticleSystem _chips, _sparkles;

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
            if (sparkle && sparkleSprite != null) mat.mainTexture = sparkleSprite.texture;
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
