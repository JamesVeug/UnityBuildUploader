using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Reorderable list of email addresses. Used for the CC and BCC fields of
    /// <see cref="EmailSendMailAction"/>.
    /// </summary>
    public class ReorderableListOfEmailRecipients : InternalReorderableList<string>
    {
        private Context m_context;
        private Dictionary<int, bool> m_showFormattedEmails = new Dictionary<int, bool>();

        public ReorderableListOfEmailRecipients(Context context)
        {
            m_context = context;
        }

        protected override void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        {
            string element = list[index] ?? "";

            const float labelWidth = 50f;
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            GUI.Label(labelRect, new GUIContent("Email", "Recipient email address for this CC/BCC entry. Supports format tokens."));
            Rect fieldRect = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
            if (EditorUtils.FormatStringTextField(fieldRect, ref element, m_showFormattedEmails, index, m_context, "e.g. teammate@studio.com"))
            {
                list[index] = element;
                dirty = true;
            }
        }

        protected override string CreateItem(int index)
        {
            return "";
        }

        protected override int CompareTo(string a, string b)
        {
            return String.Compare(a, b, StringComparison.Ordinal);
        }
    }
}
