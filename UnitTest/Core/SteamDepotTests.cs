using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Steam depot configuration without Steam: depots that merged in popups (#115) and a Depot ID field that
    /// could not be cleared or validated (#116).
    /// </summary>
    [Category("BuildUploader.Core")]
    public class SteamDepotTests
    {
        [Test]
        public void NewListItemsNeverReuseAnIdStillInUse()
        {
            var depots = new List<SteamDepot> { Depot(1, "A", 1001), Depot(2, "B", 1002), Depot(3, "C", 1003) };
            depots.RemoveAt(0);

            // Count + 1 would be 3, which 'C' still uses, so a new depot would merge with it.
            Assert.AreEqual(4, InternalReorderableList<SteamDepot>.NextItemIndex(depots));
            Assert.AreEqual(1, InternalReorderableList<SteamDepot>.NextItemIndex(new List<SteamDepot>()));
            Assert.AreEqual(3, InternalReorderableList<SteamDepot>.NextItemIndex(new List<SteamDepot> { Depot(1, "A", 1), Depot(2, "B", 2) }));
            Assert.AreEqual(2, InternalReorderableList<string>.NextItemIndex(new List<string> { "not a DropdownElement" }));
        }

        [Test]
        public void CopiesKeepTheirIds()
        {
            SteamDepot original = Depot(7, "Windows", 1001);
            var copy = new SteamDepot(original);
            Assert.AreEqual(7, copy.ID);
            Assert.AreEqual(1001, copy.Depot.DepotID);
            Assert.AreNotSame(original.Depot, copy.Depot);

            var app = new SteamApp { ID = 4, Name = "Game" };
            Assert.AreEqual(4, new SteamApp(app).ID);
        }

        [Test]
        public void PopupLabelsTellSameNamedDepotsApart()
        {
            SteamDepot first = Depot(1, "Windows", 1001);
            SteamDepot second = Depot(2, "Windows", 1002);

            Assert.AreNotEqual(first.DisplayNameWithDepotID, second.DisplayNameWithDepotID);
            Assert.AreEqual("Windows (1001)", first.DisplayNameWithDepotID);
            Assert.AreEqual("Windows (no Depot ID)", new SteamDepot(3, "Windows").DisplayNameWithDepotID);
        }

        [Test]
        public void NewDepotsStartWithoutADepotId()
        {
            Assert.AreEqual(0, new DepotVDFFile().DepotID);
            Assert.IsFalse(new SteamDepot(1, "Windows").HasDepotID);
            Assert.IsTrue(Depot(2, "Saved before #116", 999999).HasDepotID, "Depots saved with the old default still count as set.");
        }

        [Test]
        public void DepotIdFieldCanBeClearedAndIgnoresJunk()
        {
            Assert.AreEqual("", ReorderableListOfDepots.DepotIDText(0), "An unset ID shows an empty field and its placeholder.");
            Assert.AreEqual("1234561", ReorderableListOfDepots.DepotIDText(1234561));

            Assert.AreEqual(0, ReorderableListOfDepots.ParseDepotIDText("", 999999), "Clearing the field unsets the ID.");
            Assert.AreEqual(0, ReorderableListOfDepots.ParseDepotIDText("  ", 1234561));
            Assert.AreEqual(1234561, ReorderableListOfDepots.ParseDepotIDText("1234561", 0));
            Assert.AreEqual(42, ReorderableListOfDepots.ParseDepotIDText(" 42 ", 0));
            Assert.AreEqual(55, ReorderableListOfDepots.ParseDepotIDText("12a", 55));
            Assert.AreEqual(55, ReorderableListOfDepots.ParseDepotIDText("-5", 55));
        }

        [Test]
        public void AppReportsDepotsThatBlockUploads()
        {
            var app = new SteamApp();
            app.Depots.Add(Depot(1, "Unset", 0));
            app.Depots.Add(Depot(2, "Windows", 1001));
            app.Depots.Add(Depot(3, "Windows copy", 1001));
            app.Depots.Add(Depot(3, "Same internal ID", 1003));

            List<string> errors = app.GetDepotErrors();

            AssertContains(errors, "'Unset' has no Depot ID");
            AssertContains(errors, "'Windows', 'Windows copy' all use Depot ID 1001");
            AssertContains(errors, "'Windows copy', 'Same internal ID' share internal ID 3");
            Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void AppWarnsAboutSameNamedDepots()
        {
            var app = new SteamApp();
            app.Depots.Add(Depot(1, "Windows", 1001));
            app.Depots.Add(Depot(2, "Windows", 1002));

            CollectionAssert.IsEmpty(app.GetDepotErrors());
            List<string> warnings = app.GetDepotWarnings();
            AssertContains(warnings, "2 depots are named 'Windows'");
            Assert.AreEqual(1, warnings.Count, string.Join("\n", warnings));
        }

        [Test]
        public void DestinationBlocksMissingAndRepeatedDepotIds()
        {
            AssertContains(Errors(new SteamUploadDestination(480, "none", 0)), "has no Depot ID");
            AssertContains(Errors(new SteamUploadDestination(480, "none", 1001, 1001)), "Depot ID 1001 is selected more than once");

            List<string> valid = Errors(new SteamUploadDestination(480, "none", 1001, 1002));
            Assert.IsFalse(valid.Any(error => error.Contains("Depot ID")), string.Join("\n", valid));
        }

        private static SteamDepot Depot(int id, string name, int depotID)
        {
            return new SteamDepot(id, name) { Depot = new DepotVDFFile { DepotID = depotID } };
        }

        private static List<string> Errors(SteamUploadDestination destination)
        {
            // Other errors (SDK not set up, no app config) are expected here; only depot errors are checked.
            var errors = new List<GUIContent>();
            destination.TryGetErrors(errors);
            return errors.Select(error => error.text).ToList();
        }

        private static void AssertContains(List<string> messages, string expected)
        {
            Assert.IsTrue(messages.Any(message => message.Contains(expected)),
                "Expected a message containing \"" + expected + "\" in:\n" + string.Join("\n", messages));
        }
    }
}
