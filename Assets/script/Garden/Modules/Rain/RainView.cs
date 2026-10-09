using UnityEngine;

namespace Garden.Rain
{
    // Phần hiển thị của mưa: số hạt mưa và độ tối của ánh sáng theo độ lớn của mưa (RainModule.Intensity).
    // Hạt mưa là ParticleSystem do công cụ dựng cảnh tạo sẵn; ở đây chỉ điều chỉnh theo thời gian.
    public sealed class RainView : MonoBehaviour
    {
        [SerializeField] RainModule rain;
        [SerializeField] ParticleSystem particles;
        [SerializeField] Light sun;
        [SerializeField] float maxParticlesPerSecond = 420f;
        [SerializeField, Range(0.2f, 1f)] float dimmedLight = 0.6f;                       // ánh sáng còn bấy nhiêu khi mưa lớn nhất
        [SerializeField] Color rainLightColor = new Color(0.72f, 0.8f, 0.9f);              // và ngả sang màu xám xanh

        float baseIntensity;
        Color baseColor;
        bool hasBase;

        public void Configure(RainModule module, ParticleSystem system, Light light)
        {
            rain = module;
            particles = system;
            sun = light;
        }

        void OnEnable()
        {
            if (sun == null) return;
            baseIntensity = sun.intensity;
            baseColor = sun.color;
            hasBase = true;
        }

        void OnDisable() { RestoreLight(); }

        void Update()
        {
            if (rain == null) return;
            ApplyIntensity(rain.Intensity);
        }

        // Xem thử mưa ở Editor mà không cần chạy game: đặt độ lớn ngay và cho hạt mưa chạy trước một đoạn để thấy
        public void PreviewInEditor(float intensity)
        {
            if (!hasBase) OnEnable();
            ApplyIntensity(intensity);
            if (particles != null && intensity > 0.01f) particles.Simulate(2f, true, true, true);
        }

        void ApplyIntensity(float intensity)
        {
            if (particles != null)
            {
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = maxParticlesPerSecond * intensity;
                if (intensity > 0.01f && !particles.isPlaying) particles.Play();
                else if (intensity <= 0.01f && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (sun != null && hasBase)
            {
                sun.intensity = Mathf.Lerp(baseIntensity, baseIntensity * dimmedLight, intensity);
                sun.color = Color.Lerp(baseColor, rainLightColor, intensity);
            }
        }

        void RestoreLight()
        {
            if (sun == null || !hasBase) return;
            sun.intensity = baseIntensity;
            sun.color = baseColor;
        }
    }
}
