using System.Linq;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Editing a whole section of an Upload Config at once, as the menu on a section's title in the Upload tab does.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UploadConfigSectionTests
    {
        private const UploadConfig.Section Sources = UploadConfig.Section.Sources;
        private const UploadConfig.Section Destinations = UploadConfig.Section.Destinations;
        private const UploadConfig.Section Actions = UploadConfig.Section.Actions;
        private const UploadConfig.UploadActionData.UploadCompleteStatus Always = UploadConfig.UploadActionData.UploadCompleteStatus.Always;
        private const UploadConfig.UploadActionData.UploadCompleteStatus Never = UploadConfig.UploadActionData.UploadCompleteStatus.Never;
        private const UploadConfig.UploadActionData.UploadCompleteStatus IfFailed = UploadConfig.UploadActionData.UploadCompleteStatus.IfFailed;

        [Test]
        public void AddedEntriesStartAsTheChosenType()
        {
            UploadConfig config = new UploadConfig();
            config.AddEntry(Sources, typeof(FolderSource));
            config.AddEntry(Destinations, null);

            UploadConfig.SourceData source = config.Sources.Single();
            Assert.IsTrue(source.Enabled);
            Assert.IsInstanceOf<FolderSource>(source.Source);
            Assert.AreEqual(typeof(FolderSource), source.SourceType.Type);

            UploadConfig.DestinationData destination = config.Destinations.Single();
            Assert.IsTrue(destination.Enabled);
            Assert.IsNull(destination.Destination, "Choose Later leaves the type to pick from the dropdown.");
        }

        [Test]
        public void AddedActionsRunAlwaysWithTheirStopTaskDefault()
        {
            UploadConfig config = new UploadConfig();
            config.AddEntry(Actions, typeof(InvokeStaticMethodAction));
            config.AddEntry(Actions, null);

            UploadConfig.UploadActionData action = config.PostActions[0];
            Assert.IsInstanceOf<InvokeStaticMethodAction>(action.UploadAction);
            Assert.AreEqual(typeof(InvokeStaticMethodAction), action.ActionType.Type);
            Assert.AreEqual(Always, action.WhenToExecute);
            Assert.AreEqual(action.UploadAction.DefaultStopTaskIfFailed, action.StopTaskIfFailed);
            Assert.AreEqual(Always, config.PostActions[1].WhenToExecute);
        }

        [Test]
        public void EnableAllAndDisableAllSetEveryEntry()
        {
            UploadConfig config = new UploadConfig();
            config.AddSource(new UploadConfig.SourceData(new FileSource(), false));
            config.AddSource(new UploadConfig.SourceData(new FolderSource()));

            config.SetAllEnabled(Sources, true);
            Assert.AreEqual(2, config.GetEntryCount(Sources, true));

            config.SetAllEnabled(Sources, false);
            Assert.AreEqual(2, config.GetEntryCount(Sources, false));
        }

        [Test]
        public void EnablingActionsOnlyChangesTheOnesThatNeverRun()
        {
            UploadConfig config = new UploadConfig();
            config.AddPostAction(new InvokeStaticMethodAction(), Never);
            config.AddPostAction(new InvokeStaticMethodAction(), IfFailed);

            config.SetAllEnabled(Actions, true);
            Assert.AreEqual(Always, config.PostActions[0].WhenToExecute);
            Assert.AreEqual(IfFailed, config.PostActions[1].WhenToExecute, "An action that already runs keeps its condition.");

            config.SetAllEnabled(Actions, false);
            Assert.IsTrue(config.PostActions.All(a => a.WhenToExecute == Never));
        }

        [Test]
        public void RemoveDisabledKeepsTheEnabledEntries()
        {
            UploadConfig config = new UploadConfig();
            config.AddSource(new UploadConfig.SourceData(new FileSource(), false));
            config.AddSource(new UploadConfig.SourceData(new FolderSource()));
            config.AddPostAction(new InvokeStaticMethodAction(), Never);
            config.AddPostAction(new InvokeStaticMethodAction(), IfFailed);

            config.RemoveDisabledEntries(Sources);
            config.RemoveDisabledEntries(Actions);

            Assert.IsInstanceOf<FolderSource>(config.Sources.Single().Source);
            Assert.AreEqual(IfFailed, config.PostActions.Single().WhenToExecute);

            config.ClearEntries(Sources);
            Assert.AreEqual(0, config.GetEntryCount(Sources));
            Assert.AreEqual(1, config.GetEntryCount(Actions), "Clearing one section leaves the others.");
        }

        [Test]
        public void MovedEntriesKeepTheOthersInOrder()
        {
            UploadConfig config = new UploadConfig();
            config.AddSource(new UploadConfig.SourceData(new FileSource()));
            config.AddSource(new UploadConfig.SourceData(new FolderSource()));
            config.AddSource(new UploadConfig.SourceData(new FileSource(), false));
            UploadConfig.SourceData first = config.Sources[0];
            UploadConfig.SourceData second = config.Sources[1];
            UploadConfig.SourceData third = config.Sources[2];

            Assert.IsTrue(config.MoveEntry(Sources, first, 2));
            CollectionAssert.AreEqual(new[] { second, third, first }, config.Sources);

            Assert.IsTrue(config.MoveEntry(Sources, first, config.GetEntryIndex(Sources, first) - 1));
            CollectionAssert.AreEqual(new[] { second, first, third }, config.Sources);

            Assert.IsTrue(config.MoveEntry(Sources, third, 0));
            CollectionAssert.AreEqual(new[] { third, second, first }, config.Sources);
        }

        [Test]
        public void MovingPastEitherEndDoesNothing()
        {
            UploadConfig config = new UploadConfig();
            config.AddPostAction(new InvokeStaticMethodAction(), Always);
            config.AddPostAction(new InvokeStaticMethodAction(), Never);
            UploadConfig.UploadActionData first = config.PostActions[0];
            UploadConfig.UploadActionData last = config.PostActions[1];

            Assert.IsFalse(config.MoveEntry(Actions, first, -1));
            Assert.IsFalse(config.MoveEntry(Actions, last, 2));
            Assert.IsFalse(config.MoveEntry(Actions, new UploadConfig.UploadActionData(), 0), "An entry from another config isn't moved in.");
            CollectionAssert.AreEqual(new[] { first, last }, config.PostActions);
        }

        [Test]
        public void CopiedEntriesPasteIntoTheSameSectionOfAnotherConfig()
        {
            UploadConfig copiedFrom = new UploadConfig();
            copiedFrom.AddDestination(new LocalPathDestination("C:/Builds/Copied"));
            copiedFrom.AddDestination(new UploadConfig.DestinationData(new LocalPathDestination("D:/Builds/Disabled"), false));
            string copied = copiedFrom.CopyEntries(Destinations);

            UploadConfig pastedTo = new UploadConfig();
            pastedTo.AddDestination(new LocalPathDestination("E:/Builds/Existing"));
            Assert.AreEqual(2, pastedTo.PasteEntries(Destinations, copied));

            Assert.AreEqual(3, pastedTo.Destinations.Count, "Pasted entries are added after the existing ones.");
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(JSON.SerializeObject(copiedFrom.Destinations[i].Serialize()),
                    JSON.SerializeObject(pastedTo.Destinations[i + 1].Serialize()));
            }
            Assert.IsFalse(pastedTo.Destinations[2].Enabled);
            Assert.AreNotSame(copiedFrom.Destinations[0].Destination, pastedTo.Destinations[1].Destination);
        }

        [Test]
        public void OnlyEntriesCopiedFromTheSameSectionCanBePasted()
        {
            UploadConfig config = new UploadConfig();
            config.AddSource(new UploadConfig.SourceData(new FolderSource()));
            string copied = config.CopyEntries(Sources);

            Assert.IsTrue(UploadConfig.CanPasteEntries(Sources, copied));
            Assert.IsFalse(UploadConfig.CanPasteEntries(Destinations, copied));
            Assert.IsFalse(UploadConfig.CanPasteEntries(Sources, "buildUploaderSection is not JSON"));
            Assert.IsFalse(UploadConfig.CanPasteEntries(Sources, ""));
            Assert.IsFalse(UploadConfig.CanPasteEntries(Sources, null));

            Assert.AreEqual(0, config.PasteEntries(Destinations, copied));
            Assert.AreEqual(0, config.Destinations.Count);
        }
    }
}
