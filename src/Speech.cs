using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using WinSpeech = global::Windows.Media.SpeechSynthesis;
using WinStreams = global::Windows.Storage.Streams;

namespace WowLexikon
{
    /// <summary>Aussprache über die in Windows installierten Stimmen, plus echte Aufnahmen aus dem Wörterbuch.</summary>
    class Speech
    {
        SoundPlayer player;
        System.Windows.Media.MediaPlayer media;

        public static List<WinSpeech.VoiceInformation> Voices()
        {
            var r = new List<WinSpeech.VoiceInformation>();
            foreach (var v in WinSpeech.SpeechSynthesizer.AllVoices) r.Add(v);
            return r;
        }

        public static WinSpeech.VoiceInformation PickVoice(string lang, string preferredId)
        {
            string prefix = lang.Split('-')[0].ToLowerInvariant();
            WinSpeech.VoiceInformation first = null;
            foreach (var v in WinSpeech.SpeechSynthesizer.AllVoices)
            {
                if (!v.Language.ToLowerInvariant().StartsWith(prefix)) continue;
                if (!string.IsNullOrEmpty(preferredId) && v.Id == preferredId) return v;
                if (first == null) first = v;
            }
            return first;
        }

        public static bool HasVoiceFor(string lang) { return PickVoice(lang, null) != null; }

        public async Task Speak(string text, string lang, string voiceId, double rate)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            using (var syn = new WinSpeech.SpeechSynthesizer())
            {
                var v = PickVoice(lang, voiceId);
                if (v != null) syn.Voice = v;
                try { syn.Options.SpeakingRate = Math.Max(0.5, Math.Min(2.0, rate)); } catch { }
                var stream = await WinRt.Await(syn.SynthesizeTextToStreamAsync(text));
                var reader = new WinStreams.DataReader(stream.GetInputStreamAt(0));
                uint n = await WinRt.Await(reader.LoadAsync((uint)stream.Size));
                var wav = new byte[n];
                reader.ReadBytes(wav);
                Stop();
                player = new SoundPlayer(new MemoryStream(wav));
                player.Play();
            }
        }

        /// <summary>Spielt eine echte Aussprache-Aufnahme (MP3) aus dem Internet-Wörterbuch.</summary>
        public void PlayUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            Stop();
            media = new System.Windows.Media.MediaPlayer();
            media.Open(new Uri(url));
            media.Play();
        }

        public void Stop()
        {
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (media != null) { media.Stop(); media.Close(); media = null; }
        }
    }
}
