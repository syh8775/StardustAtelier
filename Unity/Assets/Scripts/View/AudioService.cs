using UnityEngine;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 소리 재생(기획서 13.5). 효과음 12종과 배경음 2곡을 씁니다. 진동은 쓰지 않습니다.
    // 켜기/끄기는 설정(save.json의 settings)을 따르며, 바꾸면 바로 반영합니다.
    public sealed class AudioService : MonoBehaviour
    {
        public AudioClip swap, swapInvalid, match, rocket, bomb, meteor, prism, obstacle, skill, clear, fail, button;
        public AudioClip bgmMap, bgmGame;
        private AudioSource sfxSource, bgmSource;
        // 설정 음량(0~1). 1이 기본 음량입니다.
        private float sfxVolume = 1f, bgmVolume = 1f;
        private const float BgmBase = 0.45f;

        private void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>(); sfxSource.playOnAwake = false;
            bgmSource = gameObject.AddComponent<AudioSource>(); bgmSource.playOnAwake = false;
            bgmSource.loop = true; bgmSource.volume = BgmBase*bgmVolume;
        }
        public void Apply(float bgm, float sfx)
        {
            bgmVolume = Mathf.Clamp01(bgm); sfxVolume = Mathf.Clamp01(sfx);
            if (bgmSource != null) bgmSource.volume = BgmBase*bgmVolume;
        }
        public void Play(AudioClip clip)
        {
            if (sfxVolume <= 0 || clip == null || sfxSource == null) return;
            sfxSource.pitch = 1f; sfxSource.PlayOneShot(clip,sfxVolume);
        }
        // 매치음은 연쇄 단계가 올라갈수록 반음씩 높이고, 6단계부터는 같은 높이입니다.
        // 높이를 바꾼 소리가 다른 효과음과 섞이지 않도록 전용 재생기를 따로 씁니다.
        private AudioSource matchSource;
        public void PlayMatch(int step)
        {
            if (sfxVolume <= 0 || match == null) return;
            if (matchSource == null) { matchSource = gameObject.AddComponent<AudioSource>(); matchSource.playOnAwake = false; }
            int semitones = Mathf.Clamp(step,1,6)-1;
            matchSource.pitch = Mathf.Pow(2f,semitones/12f);
            matchSource.PlayOneShot(match,sfxVolume);
        }
        // 같은 곡이 이미 나오고 있으면 처음부터 다시 틀지 않습니다.
        public void PlayBgm(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;
            bgmSource.clip = clip; bgmSource.volume = BgmBase*bgmVolume; bgmSource.Play();
        }
    }
}
