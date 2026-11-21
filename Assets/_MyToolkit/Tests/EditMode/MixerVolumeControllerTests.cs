using MyToolkit.Audio;
using NUnit.Framework;

namespace MyToolkit.Tests.Audio
{
    public sealed class MixerVolumeControllerTests
    {
        [Test]
        public void LinearToDecibels_FullVolume_IsZeroDb() =>
            Assert.AreEqual(0f, MixerVolumeController.LinearToDecibels(1f), 0.0001f);

        [Test]
        public void LinearToDecibels_HalfVolume_IsAboutMinusSixDb() =>
            Assert.AreEqual(-6.02f, MixerVolumeController.LinearToDecibels(0.5f), 0.01f);

        [Test]
        public void LinearToDecibels_ZeroOrTiny_ClampsToSilence()
        {
            Assert.AreEqual(-80f, MixerVolumeController.LinearToDecibels(0f));
            Assert.AreEqual(-80f, MixerVolumeController.LinearToDecibels(0.00001f));
        }
    }
}
