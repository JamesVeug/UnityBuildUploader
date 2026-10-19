using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    public abstract class InternalReorderableList<T>
    {
        protected List<T> list;
        protected bool dirty;

        private bool m_showFolderOut;
        private ReorderableList reorderableList;
        private string header = "";
        private Action<T> addCallback;

        public void Initialize(List<T> listReference, string listHeader, bool foldoutStartsOpen, Action<T> onAddCallback=null)
        {
            m_showFolderOut = foldoutStartsOpen;
            header = listHeader;
            addCallback = onAddCallback;
            list = listReference;

            reorderableList = new ReorderableList(listReference, typeof(T), true, true, true, true);
            reorderableList.headerHeight = 0;
            reorderableList.drawElementCallback = DrawItem;
            reorderableList.onAddCallback = AddCallback;
            reorderableList.onRemoveCallback = RemoveCallback;
            reorderableList.onChangedCallback = ChangedCallback;
        }

        public bool OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope("RL Header"))
            {
                GUILayout.Space(5);
                m_showFolderOut = DrawHeader();
            }

            if (m_showFolderOut)
            {
                reorderableList.DoLayoutList();
            }

            if (dirty)
            {
                dirty = false;
                return true;
            }
            return false;
        }

        protected abstract void DrawItem(Rect rect, int index, bool isActive, bool isFocused);
        protected abstract T CreateItem(int index);

        protected virtual bool DrawHeader()
        {
            string headerText = $"{header ?? ""} ({list.Count})";
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(headerText), EditorStyles.foldout);

            // Draw the foldout
            m_showFolderOut = EditorGUI.Foldout(rect, m_showFolderOut, headerText, true);

            // Handle right-click context menu
            Event evt = Event.current;
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                var menu = ContextMenu(evt);
                menu.ShowAsContext();
                evt.Use();
            }

            return m_showFolderOut;
        }

        protected virtual GenericMenu ContextMenu(Event evt)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear"), false, () =>
            {
                list.Clear();
                dirty = true;
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Order by Ascending"), false, () =>
            {
                list.Sort(CompareTo);
                dirty = true;
            });
            menu.AddItem(new GUIContent("Order by Descending"), false, () =>
            {
                list.Sort(CompareTo);
                list.Reverse();
                dirty = true;
            });
            
            return menu;
        }

        /// <summary>
        /// Index passed to CreateItem, which subclasses use as the new entry's DropdownElement Id. Configs save those
        /// Ids, so never hand out one still in the list: Count + 1 did after a delete and merged two entries (#115).
        /// </summary>
        internal static int NextItemIndex(List<T> items)
        {
            int next = items.Count + 1;
            foreach (T item in items)
            {
                if (item is DropdownElement element && element.Id >= next)
                {
                    next = element.Id + 1;
                }
            }

            return next;
        }

        private void AddCallback(ReorderableList l)
        {
            l.index++;
            T depot = CreateItem(NextItemIndex(list));
            list.Add(depot);
            dirty = true;

            addCallback?.Invoke(depot); 
        }
        
        private void RemoveCallback(ReorderableList list1)
        {
            list.RemoveAt(list1.index);
            dirty = true;
        }

        private void ChangedCallback(ReorderableList list1)
        {
            dirty = true;
        }
        
        public void SetHeaderText(string newHeader)
        {
            header = newHeader;
        }
        
        protected virtual int CompareTo(T a, T b)
        {
            return String.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }
    }
}