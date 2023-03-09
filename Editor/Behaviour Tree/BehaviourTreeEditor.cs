using UnityEngine;
using UnityEditor;

namespace Zlitz.AI
{
    [CustomEditor(typeof(BehaviourTree))]
    public class BehaviourTreeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Editor"))
            {
                BehaviourTreeEditorWindow.ShowWindow((BehaviourTree)serializedObject.targetObject);
            }
        }
    }
}
