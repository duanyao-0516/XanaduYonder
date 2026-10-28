using UnityEngine;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal sealed class MinesweeperFeedback : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip reveal, flag, win, loss;
        private void Awake()
        {
            source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.volume=.18f;
            reveal=Tone("Minesweeper reveal",new[]{440f},.045f);
            flag=Tone("Minesweeper flag",new[]{660f},.045f);
            win=Tone("Minesweeper win",new[]{523f,659f,784f},.070f);
            loss=Tone("Minesweeper loss",new[]{130f},.180f);
        }
        public void Play(MinesweeperState state, bool marking, bool sound, bool haptics, GameFeedback common)
        {
            bool ended=state==MinesweeperState.Won||state==MinesweeperState.Lost;
            if(sound)
            {
                if(ended) source.Stop();
                source.PlayOneShot(state==MinesweeperState.Won?win:state==MinesweeperState.Lost?loss:marking?flag:reveal);
            }
            if(marking||ended) common?.Pulse(haptics);
        }
        public void Stop() { if(source!=null) source.Stop(); }
        private static AudioClip Tone(string name,float[] frequencies,float duration)
        {
            const int rate=22050;
            int length=Mathf.CeilToInt(duration*rate);
            var samples=new float[length*frequencies.Length];
            for(int tone=0;tone<frequencies.Length;tone++)
            for(int i=0;i<length;i++) samples[tone*length+i]=Mathf.Sin(2*Mathf.PI*frequencies[tone]*i/rate)*Mathf.Sin(Mathf.PI*i/length)*.5f;
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }
        private void OnDisable() { Stop(); }
        private void OnDestroy()
        {
            Stop();
            foreach(var clip in new[]{reveal,flag,win,loss}) if(clip!=null) Destroy(clip);
        }
    }
}
