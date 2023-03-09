using UnityEngine;
using UnityEditor;

using Zlitz.Utilities;

namespace Zlitz.AI
{
    [CustomPropertyDrawer(typeof(BehaviourTreeRunner))]
    public class BehaviourTreeRunnerPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float res = EditorGUIUtility.singleLineHeight;
            SerializedProperty instantiatedProperty = property.FindPropertyRelative("m_instantiatedTree");
            
            if (property.isExpanded && instantiatedProperty.objectReferenceValue != null)
            {
                SerializedObject serializedInstantiatedTree = new SerializedObject(instantiatedProperty.objectReferenceValue);

                SerializedProperty propertiesProperty = serializedInstantiatedTree.FindProperty("m_properties");
                SerializedProperty nodesProperty      = serializedInstantiatedTree.FindProperty("m_nodes");

                for (int i = 0; i < propertiesProperty.arraySize; i++)
                {
                    SerializedProperty propertyProperty = propertiesProperty.GetArrayElementAtIndex(i);
                    BehaviourTree.Property treeProperty = propertyProperty.GetValue<BehaviourTree.Property>();

                    SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(treeProperty.nodeId).FindPropertyRelative(treeProperty.propertyName);

                    res += EditorGUI.GetPropertyHeight(nodeProperty, new GUIContent(treeProperty.displayName)) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return res;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height = EditorGUIUtility.singleLineHeight;
            position.width -= 20.0f;

            SerializedProperty blueprintProperty    = property.FindPropertyRelative("m_blueprintTree");
            SerializedProperty instantiatedProperty = property.FindPropertyRelative("m_instantiatedTree");

            blueprintProperty.serializedObject.Update();
            EditorGUI.PropertyField(position, blueprintProperty, label);
            blueprintProperty.serializedObject.ApplyModifiedProperties();

            position.width += 20.0f;

            Rect expandButtonRect = position;
            expandButtonRect.x += position.width - 20.0f;
            expandButtonRect.width = 20.0f;
            if (GUI.Button(expandButtonRect, new GUIContent(property.isExpanded ? "-" : "+")))
            {
                property.isExpanded = !property.isExpanded;
            }

            if (property.isExpanded && instantiatedProperty.objectReferenceValue != null)
            {
                SerializedObject serializedInstantiatedTree = new SerializedObject(instantiatedProperty.objectReferenceValue);

                SerializedProperty propertiesProperty = serializedInstantiatedTree.FindProperty("m_properties");
                SerializedProperty nodesProperty      = serializedInstantiatedTree.FindProperty("m_nodes");

                EditorGUI.indentLevel++;

                for (int i = 0; i < propertiesProperty.arraySize; i++)
                {
                    SerializedProperty     propertyProperty = propertiesProperty.GetArrayElementAtIndex(i);
                    BehaviourTree.Property treeProperty     = propertyProperty.GetValue<BehaviourTree.Property>();

                    SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(treeProperty.nodeId).FindPropertyRelative(treeProperty.propertyName);

                    position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                    position.height = EditorGUI.GetPropertyHeight(nodeProperty, new GUIContent(treeProperty.displayName));
                    nodeProperty.serializedObject.Update();
                    EditorGUI.PropertyField(position, nodeProperty, new GUIContent(treeProperty.displayName));
                    nodeProperty.serializedObject.ApplyModifiedProperties();
                }

                EditorGUI.indentLevel--;
            }
        }
    }
}
