using NUnit.Framework;
using Status = Wireframe.UploadConfig.UploadActionData.UploadCompleteStatus;
using Trigger = Wireframe.UploadConfig.UploadActionData.UploadTrigger;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Saved profiles name their sources, modifiers, destinations and actions by Type.FullName. Types outside
    /// the core assembly (integrations, these test doubles, the user's own) must reload from that name.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class SavedTypeResolutionTests
    {
        [Test]
        public void TypesFromOtherAssembliesReloadWithTheirData()
        {
            var config = new UploadConfig();
            config.AddSource(new FolderSource("unused"));
            config.AddDestination(new UnitTestFailingDestination());
            config.AddPostAction(new UnitTestRecordingAction { Label = "config action" }, Status.IfFailed);
            var profile = new UploadProfile { GUID = "saved-types", ProfileName = "Saved type resolution" };
            profile.UploadConfigs.Add(config);
            profile.Actions.Add(new UploadConfig.UploadActionData(new UnitTestRecordingAction { Label = "task action" },
                Status.IfSuccessful, Trigger.OnTaskStarted));

            string json = JSON.SerializeObject(UploadProfileSavedData.FromUploadProfile(profile));
            UploadProfile reloaded = UploadProfileSavedData.FromJSON(json).ToUploadProfile();

            StringAssert.Contains("\"" + typeof(UnitTestRecordingAction).FullName + "\"", json, "Types are still saved by their full name.");
            StringAssert.DoesNotContain(typeof(UnitTestRecordingAction).Assembly.GetName().Name, json, "The saved format must not change.");

            Assert.AreEqual(1, reloaded.Actions.Count);
            var taskAction = reloaded.Actions[0].UploadAction as UnitTestRecordingAction;
            Assert.IsNotNull(taskAction, "The task action did not reload: " + json);
            Assert.AreEqual("task action", taskAction.Label);
            Assert.AreEqual(typeof(UnitTestRecordingAction), reloaded.Actions[0].ActionType.Type);
            Assert.AreEqual(Status.IfSuccessful, reloaded.Actions[0].WhenToExecute);
            CollectionAssert.AreEqual(new[] { Trigger.OnTaskStarted }, reloaded.Actions[0].Triggers);

            UploadConfig reloadedConfig = reloaded.UploadConfigs[0];
            Assert.IsInstanceOf<FolderSource>(reloadedConfig.Sources[0].Source);
            Assert.IsInstanceOf<UnitTestFailingDestination>(reloadedConfig.Destinations[0].Destination, "The destination did not reload.");
            Assert.AreEqual(typeof(UnitTestFailingDestination), reloadedConfig.Destinations[0].DestinationType.Type);
            var configAction = reloadedConfig.PostActions[0].UploadAction as UnitTestRecordingAction;
            Assert.IsNotNull(configAction, "The config action did not reload.");
            Assert.AreEqual("config action", configAction.Label);
        }

        [Test]
        public void FindTypeLooksBeyondTheCoreAssembly()
        {
            Assert.AreEqual(typeof(FolderSource), InternalUtils.FindType(typeof(FolderSource).FullName));
            Assert.AreEqual(typeof(UnitTestRecordingAction), InternalUtils.FindType(typeof(UnitTestRecordingAction).FullName));
            Assert.AreEqual(typeof(SavedTypeResolutionTests), InternalUtils.FindType(typeof(SavedTypeResolutionTests).FullName),
                "Types that are not registered are still found in the loaded assemblies.");
            Assert.IsNull(InternalUtils.FindType("Wireframe.UnitTest.NoSuchType"));
            Assert.IsNull(InternalUtils.FindType(null));
        }
    }
}
