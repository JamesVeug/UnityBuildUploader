using System;
using UnityEngine;

namespace Wireframe
{
    public class ReorderableListOfDepots : InternalReorderableList<SteamDepot>
    {
        protected override void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        {
            SteamDepot element = list[index];

            float labelWidth = 50;
            float fieldWidth = Mathf.Min(100, rect.width / 2);
            Rect rect1 = new Rect(rect.x, rect.y, labelWidth, rect.height);
            GUI.Label(rect1, new GUIContent("Name", "Display name for this depot. UI only — not sent to Steam."));
            rect1.x += rect1.width;
            rect1.width = fieldWidth;
            string n = EditorUtils.PlaceholderTextField(rect1, element.Name, "e.g. Windows Content");
            if (n != element.Name)
            {
                element.Name = n;
                dirty = true;
            }

            rect1.x += rect1.width;
            rect1.width = 70;
            using (new ColorScope(element.HasDepotID ? GUI.color : Color.red))
            {
                string tooltip = "Numeric Steam depot ID from your app's depot configuration in Steamworks.";
                if (!element.HasDepotID)
                {
                    tooltip += "\n\nRequired: uploads to this depot are blocked until it is set.";
                }
                GUI.Label(rect1, new GUIContent("Depot ID", tooltip));
            }

            rect1.x += rect1.width;
            rect1.width = fieldWidth;
            string textField = EditorUtils.PlaceholderTextField(rect1, DepotIDText(element.Depot.DepotID), "e.g. 1234561");
            int value = ParseDepotIDText(textField, element.Depot.DepotID);
            if (value != element.Depot.DepotID)
            {
                element.Depot.DepotID = value;
                dirty = true;
            }
        }

        /// <summary>
        /// Text for the Depot ID field. Empty while unset, so the field can be cleared and shows its placeholder (#116).
        /// </summary>
        internal static string DepotIDText(int depotID)
        {
            return depotID > 0 ? depotID.ToString() : "";
        }

        /// <summary>
        /// The Depot ID after an edit. Clearing the field unsets it; text that is not a positive number is ignored.
        /// </summary>
        internal static int ParseDepotIDText(string text, int currentDepotID)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return int.TryParse(text.Trim(), out int value) && value > 0 ? value : currentDepotID;
        }

        protected override SteamDepot CreateItem(int index)
        {
            return new SteamDepot(index, "");
        }

        protected override int CompareTo(SteamDepot a, SteamDepot b)
        {
            return String.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        }
    }
}