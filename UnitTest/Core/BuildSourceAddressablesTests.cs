using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Build Addressables and Clean Addressables on build sources: saved with the source, kept when Addressables is
    /// not installed, and validated. Uses a stand-in for the Addressables integration, so it needs no package.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class BuildSourceAddressablesTests
    {
        private IAddressablesSupport m_previousSupport;

        [SetUp]
        public void SetUp()
        {
            m_previousSupport = AddressablesSupport.Implementation;
        }

        [TearDown]
        public void TearDown()
        {
            AddressablesSupport.Implementation = m_previousSupport;
        }

        [Test]
        public void BuildAddressablesIsSavedWithTheSource()
        {
            var source = new BuildConfigSource();
            source.SetBuildAddressables(true, true);

            BuildConfigSource reloaded = Reload(source.Serialize());

            Assert.IsTrue(reloaded.BuildAddressables);
            Assert.IsTrue(reloaded.CleanAddressables);
        }

        [Test]
        public void SourcesSavedBeforeBuildAddressablesLoadWithItOff()
        {
            Dictionary<string, object> data = new BuildConfigSource(null, true).Serialize();
            data.Remove("BuildAddressables");
            data.Remove("CleanAddressables");

            BuildConfigSource reloaded = Reload(data);

            Assert.IsFalse(reloaded.BuildAddressables);
            Assert.IsFalse(reloaded.CleanAddressables);
        }

        [Test]
        public void BuildAddressablesWithoutTheIntegrationIsAnErrorButIsKept()
        {
            AddressablesSupport.Implementation = null;
            var source = new BuildConfigSource();
            source.SetBuildAddressables(true);

            Assert.IsTrue(Errors(source).Any(error => error.Contains(AddressablesSupport.Requirement)), string.Join("\n", Errors(source)));
            Assert.IsTrue(Reload(source.Serialize()).BuildAddressables, "Opening the project without Addressables must not lose the setting.");

            source.SetBuildAddressables(false);
            Assert.IsFalse(Errors(source).Any(error => error.Contains("Addressables")), "Only an enabled toggle needs Addressables.");
        }

        [Test]
        public void ProjectWithoutAddressablesSettingsIsAnError()
        {
            var support = new UnitTestAddressablesSupport { SettingsExist = false };
            AddressablesSupport.Implementation = support;
            var source = new BuildConfigSource();
            source.SetBuildAddressables(true);

            Assert.IsTrue(Errors(source).Any(error => error.Contains("no Addressables settings")), string.Join("\n", Errors(source)));

            support.SettingsExist = true;
            Assert.IsFalse(Errors(source).Any(error => error.Contains("Addressables")), string.Join("\n", Errors(source)));
        }

        [Test]
        public void BuildAddressablesIsRedundantWhenTheProjectBuildsContentWithThePlayer()
        {
            var support = new UnitTestAddressablesSupport { BuildsWithPlayerByDefault = true };
            AddressablesSupport.Implementation = support;
            var source = new BuildConfigSource();
            source.SetBuildAddressables(true);

            Assert.IsTrue(Warnings(source).Any(warning => warning.Contains("redundant")));

            support.BuildsWithPlayerByDefault = false;
            Assert.IsFalse(Warnings(source).Any(warning => warning.Contains("redundant")));
        }

        private static BuildConfigSource Reload(Dictionary<string, object> data)
        {
            var reloaded = new BuildConfigSource();
            reloaded.Deserialize(JSON.DeserializeObject<Dictionary<string, object>>(JSON.SerializeObject(data)));
            return reloaded;
        }

        private static List<string> Errors(AUploadSource source)
        {
            var errors = new List<GUIContent>();
            source.TryGetErrors(errors);
            return errors.Select(error => error.text).ToList();
        }

        private static List<string> Warnings(AUploadSource source)
        {
            var warnings = new List<GUIContent>();
            source.TryGetWarnings(warnings);
            return warnings.Select(warning => warning.text).ToList();
        }
    }
}
