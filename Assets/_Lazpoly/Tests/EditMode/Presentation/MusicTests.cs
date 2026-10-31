using Lazpoly.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class MusicTests
    {
        [Test]
        public void EveryTrackHasAClipLongEnoughToCrossfade()
        {
            foreach (string track in MusicPlayer.Tracks)
            {
                var clip = Resources.Load<AudioClip>(MusicPlayer.ResourcePath(track));
                Assert.IsNotNull(clip, $"missing music '{track}'; run Tools/Audio/make_music.py");
                Assert.Greater(clip.length, MusicPlayer.CrossfadeSeconds * 3f, $"'{track}' is too short to fade in and out");
            }
        }

        [Test]
        public void TrackNamesAreSnakeCaseResourcePaths()
        {
            Assert.AreEqual("Lazpoly/Music/kiyi", MusicPlayer.ResourcePath("kiyi"));
            Assert.That(MusicPlayer.Tracks, Is.Unique);
        }
    }
}
