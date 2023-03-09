using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;

using Zlitz.Utilities;

namespace Zlitz.AI
{
    public class BehaviourTreeEditorWindow : EditorWindow
    {
        private Vector2      m_scrollPos;
        private bool         m_treePropertiesFoldout = true;
        private bool         m_changed = false;
        private NodeTemplate m_clipboard;

        private BehaviourTree          m_behaviourTree;
        private BehaviourTreeEvaluator m_evaluator;

        private Dict<int, BehaviourTreeNode> m_replace;
        private List<int>                    m_replaceWithClipboard;

        private static readonly float s_nodeWidth   = 300.0f;
        private static readonly float s_nodePadding = 6.0f;
        private static readonly float s_nodeSpacing = 36.0f;
        private static readonly float s_nodeHeader  = 36.0f;
        private static readonly float s_nodeFooter  = 18.0f;
        private static readonly float s_nodeBorder  = 3.0f;

        private static Color s_compositeNodeNameColor = new Color(0.65f, 0.45f, 0.75f);
        private static Color s_decoratorNodeNameColor = new Color(0.35f, 0.75f, 0.55f);
        private static Color s_actionNodeNameColor    = new Color(0.75f, 0.35f, 0.35f);
        private static Color s_idleStateColor         = new Color(0.55f, 0.55f, 0.55f);
        private static Color s_runningStateColor      = new Color(0.45f, 0.85f, 0.95f);
        private static Color s_successStateColor      = new Color(0.45f, 0.95f, 0.45f);
        private static Color s_failureStateColor      = new Color(0.85f, 0.15f, 0.15f);

        public static void ShowWindow(BehaviourTree behaviourTree)
        {
            BehaviourTreeEditorWindow window = GetWindow<BehaviourTreeEditorWindow>("Behaviour Tree Editor");
            window.m_behaviourTree = behaviourTree;
        }

        private void OnEnable()
        {
            m_replace = new Dict<int, BehaviourTreeNode>();
            m_replaceWithClipboard = new List<int>();
            if (Selection.activeObject is BehaviourTree behaviourTree)
            {
                m_behaviourTree = behaviourTree;
            }
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out BehaviourTreeEvaluator evaluator) && evaluator.behaviourTree == m_behaviourTree) 
            {
                m_evaluator = evaluator;
            }
            Selection.selectionChanged += p_OnSelectionChanged;
            Undo.undoRedoPerformed += p_OnUndoRedo;
        }

        private void OnDestroy()
        {
            Selection.selectionChanged -= p_OnSelectionChanged;
            Undo.undoRedoPerformed -= p_OnUndoRedo;
        }

        private void Update()
        {
            if (m_evaluator != null && Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            bool changed = m_changed;
            p_DrawTreeProperties(ref changed);
            p_DrawTree(ref changed);

            if (changed)
            {
                if (m_behaviourTree != null)
                {
                    SerializedProperty versionProperty = new SerializedObject(m_behaviourTree).FindProperty("m_version");
                    versionProperty.serializedObject.Update();
                    versionProperty.stringValue = Guid.NewGuid().ToString();
                    versionProperty.serializedObject.ApplyModifiedProperties();

                    EditorUtility.SetDirty(m_behaviourTree);
                    AssetDatabase.SaveAssets();
                }
            }

            m_changed = false;

            EditorGUILayout.EndHorizontal();
        }

        private void p_OnSelectionChanged()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out BehaviourTreeEvaluator evaluator) && evaluator.behaviourTree == m_behaviourTree)
            {
                m_evaluator = evaluator;
                Repaint();
            }
        }

        private void p_OnUndoRedo()
        {
            m_changed = true;
            Repaint();
        }

        private void p_DrawTreeProperties(ref bool changed)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(240.0f));

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Behaviour Tree");
            EditorGUILayout.ObjectField(GUIContent.none, m_behaviourTree, typeof(BehaviourTree), true);
            EditorGUI.EndDisabledGroup();

            if (m_behaviourTree != null)
            {
                EditorGUI.BeginDisabledGroup(Application.isPlaying);

                SerializedProperty propertiesProperty = new SerializedObject(m_behaviourTree).FindProperty("m_properties");
                propertiesProperty.serializedObject.Update();

                List<string> currentNames = new List<string>();
                for (int i = 0; i < propertiesProperty.arraySize; i++)
                {
                    currentNames.Add(propertiesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("displayName").stringValue);
                }

                EditorGUILayout.Space();
                m_treePropertiesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(m_treePropertiesFoldout, new GUIContent("Tree Properties"));
                if (m_treePropertiesFoldout)
                {
                    GUIStyle nameStyle = new GUIStyle();
                    nameStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
                    nameStyle.padding = new RectOffset(6, 0, 2, 0);

                    int remove = -1;

                    EditorGUI.BeginChangeCheck();
                    for (int i = 0; i < propertiesProperty.arraySize; i++)
                    {
                        SerializedProperty propertyProperty     = propertiesProperty.GetArrayElementAtIndex(i);
                        SerializedProperty nameProperty         = propertyProperty.FindPropertyRelative("displayName");
                        SerializedProperty propertyIdProperty   = propertyProperty.FindPropertyRelative("propertyId");
                        SerializedProperty nodeIdProperty       = propertyProperty.FindPropertyRelative("nodeId");
                        SerializedProperty propertyNameProperty = propertyProperty.FindPropertyRelative("propertyName");

                        EditorGUILayout.BeginHorizontal();
                        nameProperty.serializedObject.Update();
                        string newName = EditorGUILayout.TextField(nameProperty.stringValue, nameStyle);
                        changed |= newName != nameProperty.stringValue;
                        nameProperty.stringValue = newName;
                        nameProperty.serializedObject.ApplyModifiedProperties();
                        if (nameProperty.stringValue == "" || nameProperty.stringValue == null)
                        {
                            nameProperty.stringValue = "Property";
                            changed = true;
                        }
                        if (changed)
                        {
                            List<string> otherNames = new List<string>(currentNames);
                            otherNames.RemoveAt(i);
                            nameProperty.stringValue = ObjectNames.GetUniqueName(otherNames.ToArray(), nameProperty.stringValue);
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Remove", GUILayout.Width(80.0f)))
                        {
                            remove = i;
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();

                        List<int> nodeIds = m_behaviourTree.nodes.Where(n => n != null).Select(n => n.id).ToList();
                        int currentIndex = nodeIds.IndexOf(nodeIdProperty.intValue);
                        currentIndex = EditorGUILayout.Popup(currentIndex, m_behaviourTree.nodes.Where(n => n != null).Select(n => new GUIContent(n.name)).ToArray());
                        nodeIdProperty.intValue = nodeIds[currentIndex];

                        List<string> propertyDisplayNames = new List<string>();
                        List<string> propertyNames = new List<string>();

                        SerializedProperty nodeProperty = new SerializedObject(m_behaviourTree).FindProperty("m_nodes").GetArrayElementAtIndex(nodeIds[currentIndex]);
                        string nodePath = nodeProperty.propertyPath;
                        foreach (SerializedProperty p in nodeProperty)
                        {
                            if (p.name != "m_name" && p.name != "m_id" && p.name != "m_behaviourTree" && p.name != "m_exposed" && p.propertyPath.Replace(nodePath, "").Where(c => c == '.').Count() == 1)
                            {
                                propertyDisplayNames.Add(p.displayName);
                                propertyNames.Add(p.name);
                            }
                        }
                        EditorGUI.BeginDisabledGroup(propertyDisplayNames.Count <= 0);
                        currentIndex = Mathf.Max(0, propertyNames.IndexOf(propertyNameProperty.stringValue));
                        currentIndex = EditorGUILayout.Popup(currentIndex, propertyDisplayNames.Select(n => new GUIContent(n)).ToArray());
                        if (propertyNames.Count > currentIndex)
                        {
                            propertyNameProperty.stringValue = propertyNames[currentIndex];
                        }
                        EditorGUI.EndDisabledGroup();

                        EditorGUILayout.EndHorizontal();
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        changed = true;
                    }

                    if (GUILayout.Button("Add"))
                    {
                        int index = propertiesProperty.arraySize;
                        propertiesProperty.InsertArrayElementAtIndex(index);
                        SerializedProperty propertyProperty = propertiesProperty.GetArrayElementAtIndex(index);
                        SerializedProperty nameProperty = propertyProperty.FindPropertyRelative("displayName");
                        nameProperty.stringValue = ObjectNames.GetUniqueName(currentNames.ToArray(), nameProperty.stringValue);
                        changed = true;
                    }

                    if (remove >= 0)
                    {
                        propertiesProperty.DeleteArrayElementAtIndex(remove);
                    }

                    propertiesProperty.serializedObject.ApplyModifiedProperties();
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
        }

        private void p_DrawTree(ref bool changed)
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos, GUI.skin.box);
            if (m_behaviourTree != null)
            {
                Type behaviourTreeType = m_behaviourTree.GetType();
                
                MethodInfo isDescendantOf = behaviourTreeType.GetMethod("p_IsDescendantOf", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo removeMethod   = behaviourTreeType.GetMethod("p_Remove", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo replaceMethod  = behaviourTreeType.GetMethod("p_Replace", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var p in m_replace)
                {
                    Undo.RecordObject(m_behaviourTree, "Replace Node");
                    if (p.Value == null)
                    {
                        removeMethod.Invoke(m_behaviourTree, new object[] { p.Key });
                    }
                    else
                    {
                        replaceMethod.Invoke(m_behaviourTree, new object[] { p.Key, p.Value });
                    }
                    changed = true;
                }
                m_replace.Clear();

                foreach (int replaceId in m_replaceWithClipboard)
                {
                    Undo.RecordObject(m_behaviourTree, "Replace Node");
                    List<string> extNames = m_behaviourTree.nodes.Where(n => n != null && !(bool)isDescendantOf.Invoke(m_behaviourTree, new object[] { n.id, replaceId })).Select(n => n.name).ToList();
                    BehaviourTreeNode[] newNodes = m_clipboard.nodes.Select(n => n.Copy()).ToArray();
                    foreach (BehaviourTreeNode newNode in newNodes)
                    {
                        newNode.name = ObjectNames.GetUniqueName(extNames.ToArray(), newNode.name);
                        extNames.Add(newNode.name);
                    }
                    replaceMethod.Invoke(m_behaviourTree, new object[] { replaceId, newNodes[0] });
                    Set<int> validated = new Set<int>();
                    for (int i = 1; i < newNodes.Length; i++)
                    {
                        if (validated.Add(m_clipboard.parentIndices[i]))
                        {
                            MethodInfo getChildrenMethod = behaviourTreeType.GetMethod("p_GetChildren", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            BehaviourTreeNode[] children = (BehaviourTreeNode[])getChildrenMethod.Invoke(m_behaviourTree, new object[] { newNodes[m_clipboard.parentIndices[i]].id });

                            foreach (BehaviourTreeNode child in children)
                            {
                                removeMethod.Invoke(m_behaviourTree, new object[] { child.id });
                            }
                        }

                        MethodInfo addMethod = behaviourTreeType.GetMethod("p_Add", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        addMethod.Invoke(m_behaviourTree, new object[] { newNodes[i], newNodes[m_clipboard.parentIndices[i]] });
                    }
                    changed = true;
                }
                m_replaceWithClipboard.Clear();

                Handles.BeginGUI();
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);

                SerializedProperty rootIdProperty = new SerializedObject(m_behaviourTree).FindProperty("m_rootId");

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                changed |= p_DrawNode(rootIdProperty.intValue, false, false, !Application.isPlaying, m_behaviourTree.root.isComposite, false, Optional<Vector2>.Empty());
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(s_nodeSpacing);

                Handles.EndGUI();
            }
            EditorGUILayout.EndScrollView();
        }

        private bool p_DrawNode(int id, bool movableLeft, bool movableRight, bool editable, bool addable, bool removable, Optional<Vector2> parentPort)
        {
            bool res = false;

            BehaviourTreeNode node = m_behaviourTree.nodes[id];
            SerializedProperty nodeProperty = new SerializedObject(m_behaviourTree).FindProperty("m_nodes").GetArrayElementAtIndex(id);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(s_nodeSpacing);

            int nodeWidth = p_NodeWidth(node);
            float propertiesHeight = p_NodePropertiesHeight(nodeProperty);

            float areaWidth = Mathf.Max(0.0f, nodeWidth * (s_nodeWidth + 2.0f * s_nodePadding) + (nodeWidth - 1) * s_nodeSpacing);
            float areaHeight = s_nodeHeader + propertiesHeight + 4.0f * s_nodePadding + s_nodeFooter;

            Rect nodeRect = GUILayoutUtility.GetRect(areaWidth, areaHeight);
            nodeRect.x += 0.5f * (nodeRect.width - s_nodeWidth - 2.0f * s_nodePadding);
            nodeRect.width = s_nodeWidth + 2.0f * s_nodePadding;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && !Application.isPlaying && nodeRect.Contains(Event.current.mousePosition))
            {
                Type behaviourTreeType = m_behaviourTree.GetType();

                MethodInfo moveLeftMethod  = behaviourTreeType.GetMethod("p_MoveLeft", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo moveRightMethod = behaviourTreeType.GetMethod("p_MoveRight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                MethodInfo addParentMethod = behaviourTreeType.GetMethod("p_AddParent", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Add Parent"), false, () => {
                    Undo.RecordObject(m_behaviourTree, "Add Parent Node");
                    addParentMethod.Invoke(m_behaviourTree, new object[] { node, new ConditionNode() });
                });
                if (addable)
                {
                    menu.AddItem(new GUIContent("Add Child"), false, () => {
                        if (m_behaviourTree.nodes[id] is CompositeNode composite)
                        {
                            res = true;
                            Undo.RecordObject(m_behaviourTree, "Add Child Node");
                            composite.Add<EmptyNode>();
                        }
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Add Child"));
                }
                if (movableLeft)
                {
                    menu.AddItem(new GUIContent("Move Left"), false, () => {
                        res = true;
                        Undo.RecordObject(m_behaviourTree, "Move Node To Left");
                        moveLeftMethod.Invoke(m_behaviourTree, new object[] { node.id });
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move Left"));
                }
                if (movableRight)
                {
                    menu.AddItem(new GUIContent("Move Right"), false, () => {
                        res = true;
                        Undo.RecordObject(m_behaviourTree, "Move Node To Right");
                        moveRightMethod.Invoke(m_behaviourTree, new object[] { node.id });
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Move Right"));
                }

                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Copy"), false, () => {
                    m_clipboard = p_CopyTemplate(id);
                });
                if (m_clipboard != null)
                {
                    menu.AddItem(new GUIContent("Paste"), false, () => {
                        m_replaceWithClipboard.Add(id);
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }
                if (removable)
                {
                    menu.AddItem(new GUIContent("Remove"), false, () => {
                        m_replace[node.id] = null;
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Remove"));
                }
                menu.ShowAsContext();
            }

            if (parentPort.enabled)
            {
                Vector2 port = nodeRect.position + new Vector2(0.5f * nodeRect.width, -s_nodeBorder);
                Handles.DrawLine(parentPort.value, port);
            }

            Color borderColor = s_idleStateColor;
            if (Application.isPlaying && m_evaluator != null)
            {
                int state = p_GetNodeState(node, m_evaluator);
                borderColor = state == 0 ? s_runningStateColor : state == 1 ? s_successStateColor : state == 2 ? s_failureStateColor : s_idleStateColor;
            }
            Rect borderRect = nodeRect;
            borderRect.position -= Vector2.one * s_nodeBorder;
            borderRect.size += 2.0f * Vector2.one * s_nodeBorder;
            EditorGUI.DrawRect(borderRect, borderColor);

            EditorGUI.BeginDisabledGroup(!editable);

            Rect headerRect = nodeRect;
            float x = headerRect.x;
            headerRect.height = s_nodeHeader + 2.0f * s_nodePadding;
            EditorGUI.DrawRect(headerRect, new Color(0.1f, 0.1f, 0.1f, 1.0f));
            headerRect.position += Vector2.one * s_nodePadding;
            headerRect.width = s_nodeWidth;
            headerRect.height = 18.0f;
            GUIStyle nameStyle = new GUIStyle();
            nameStyle.normal.textColor = (
                node.isComposite ? s_compositeNodeNameColor :
                node.isDecorator ? s_decoratorNodeNameColor :
                node.isAction ? s_actionNodeNameColor :
                Color.black
            );
            nameStyle.fontStyle = FontStyle.Bold;
            node.name = EditorGUI.TextField(headerRect, node.name, nameStyle);
            if (node.name == null || node.name == "")
            {
                node.name = ObjectNames.NicifyVariableName(node.GetType().Name).Replace(" Node", "");
            }
            node.name = ObjectNames.GetUniqueName(m_behaviourTree.nodes.Where(n => n != node && n != null).Select(n => n.name).ToArray(), node.name);
            nodeProperty.serializedObject.Update();

            headerRect.x += headerRect.width;
            headerRect.width = 72.0f;
            Rect operationsRect = headerRect;

            headerRect.x = x + s_nodePadding;
            headerRect.y += 18.0f;
            headerRect.width = s_nodeWidth;
            float width = headerRect.width;
            headerRect.width = 80.0f;
            EditorGUI.LabelField(headerRect, new GUIContent("Node Type"));
            headerRect.x += 80.0f;
            headerRect.width = width - 80.0f;
            if (GUI.Button(headerRect, new GUIContent(ObjectNames.NicifyVariableName(node.GetType().Name).Replace(" Node", "")), EditorStyles.popup))
            {
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), BehaviourNodeSearchProvider.Create(t => {
                    if (t != node.GetType())
                    {
                        BehaviourTreeNode newNode = (BehaviourTreeNode)Activator.CreateInstance(t);
                        newNode.name = node.name;
                        m_replace[node.id] = newNode;
                    }
                }));
            }

            EditorGUI.EndDisabledGroup();

            Rect propertiesRect = nodeRect;
            propertiesRect.y += s_nodeHeader + 2.0f * s_nodePadding;
            propertiesRect.height = propertiesHeight + 2.0f * s_nodePadding;
            EditorGUI.DrawRect(propertiesRect, new Color(0.15f, 0.15f, 0.15f, 1.0f));
            propertiesRect.position += Vector2.one * s_nodePadding;
            propertiesRect.size -= Vector2.one * 2.0f * s_nodePadding;

            EditorGUI.BeginDisabledGroup(!editable);

            nodeProperty = new SerializedObject(m_behaviourTree).FindProperty("m_nodes").GetArrayElementAtIndex(id);
            string nodePath = nodeProperty.propertyPath;
            foreach (SerializedProperty p in nodeProperty)
            {
                if (p.name != "m_name" && p.name != "m_id" && p.name != "m_behaviourTree" && p.name != "m_exposed" && p.propertyPath.Replace(nodePath, "").Where(c => c == '.').Count() == 1)
                {
                    propertiesRect.height = EditorGUI.GetPropertyHeight(p);
                    EditorGUI.PropertyField(propertiesRect, p);
                    propertiesRect.y += propertiesRect.height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            nodeProperty.serializedObject.ApplyModifiedProperties();

            EditorGUI.EndDisabledGroup();

            if (node.exposed)
            {
                Vector2 port = nodeRect.position + new Vector2(nodeRect.width * 0.5f, nodeRect.height + s_nodeBorder);

                EditorGUILayout.BeginHorizontal();

                Type behaviourTreeType = m_behaviourTree.GetType();
                MethodInfo getChildrenMethod = behaviourTreeType.GetMethod("p_GetChildren", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                BehaviourTreeNode[] children = (BehaviourTreeNode[])getChildrenMethod.Invoke(m_behaviourTree, new object[] { id });

                int[] childrenIds = children.Select(n => n.id).ToArray();
                bool first = true;
                int index = 0;
                foreach (int childId in childrenIds)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        EditorGUILayout.Space(s_nodeSpacing);
                    }
                    res |= p_DrawNode(childId, index > 0, index < childrenIds.Length - 1, !Application.isPlaying, m_behaviourTree.nodes[childId].isComposite, node.isComposite, Optional<Vector2>.Of(port));
                    index++;
                }

                EditorGUILayout.EndHorizontal();
            }

            Rect footerRect = nodeRect;
            footerRect.y      += s_nodeHeader + 4.0f * s_nodePadding + propertiesHeight;
            footerRect.height = s_nodeFooter;
            EditorGUI.DrawRect(footerRect, new Color(0.1f, 0.1f, 0.1f, 1.0f));

            GUIStyle foldoutStyle = new GUIStyle();
            foldoutStyle.alignment         = TextAnchor.MiddleCenter;
            foldoutStyle.normal.background = null;
            foldoutStyle.normal.textColor  = new Color(0.8f, 0.8f, 0.8f, 1.0f);
            nodeProperty = new SerializedObject(m_behaviourTree).FindProperty("m_nodes").GetArrayElementAtIndex(id);
            SerializedProperty exposedProperty = nodeProperty.FindPropertyRelative("m_exposed");
            if (GUI.Button(footerRect, new GUIContent(exposedProperty.boolValue ? "v" : "..."), foldoutStyle))
            {
                exposedProperty.boolValue = !exposedProperty.boolValue;
                exposedProperty.serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.EndVertical();

            return res;
        }

        private int p_NodeWidth(BehaviourTreeNode node)
        {
            if (node != null)
            {
                if (!node.exposed)
                {
                    return 1;
                }

                if (node is CompositeNode composite)
                {
                    int width = 0;
                    foreach (BehaviourTreeNode child in composite.children)
                    {
                        width += p_NodeWidth(child);
                    }
                    return Mathf.Max(1, width);
                }

                if (node is DecoratorNode decorator)
                {
                    return Mathf.Max(1, p_NodeWidth(decorator.child));
                }

                if (node is ActionNode)
                {
                    return 1;
                }
            }

            return 0;
        }

        private float p_NodePropertiesHeight(SerializedProperty nodeProperty)
        {
            float res = -EditorGUIUtility.standardVerticalSpacing;
            string nodePath = nodeProperty.propertyPath;
            foreach (SerializedProperty p in nodeProperty)
            {
                if (p.name != "m_name" && p.name != "m_id" && p.name != "m_behaviourTree" && p.name != "m_exposed" && p.propertyPath.Replace(nodePath, "").Where(c => c == '.').Count() == 1)
                {
                    res += EditorGUI.GetPropertyHeight(p) + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            return Mathf.Max(res, 0.0f);
        }

        private int p_GetNodeState(BehaviourTreeNode node, BehaviourTreeEvaluator evaluator)
        {
            SerializedProperty treeRunnerProperty = new SerializedObject(evaluator).FindProperty("m_behaviourTree");
            if (treeRunnerProperty != null && treeRunnerProperty.GetValue() != null)
            {
                SerializedProperty instantiatedProperty = treeRunnerProperty.FindPropertyRelative("m_instantiatedTree");
                if (instantiatedProperty != null && instantiatedProperty.objectReferenceValue != null && instantiatedProperty.objectReferenceValue is BehaviourTree instantiatedTree && instantiatedTree.version == m_behaviourTree.version)
                {
                    Debug.Log("Yos");
                    return instantiatedTree.nodes[node.id].nodeState;
                }
            }

            return -1;
        }

        private NodeTemplate p_CopyTemplate(int nodeId)
        {
            List<BehaviourTreeNode> nodes         = new List<BehaviourTreeNode>();
            List<int>               parentIndices = new List<int>();

            nodes.Add(m_behaviourTree.nodes[nodeId].Copy());
            parentIndices.Add(-1);

            int index = 0;
            while (index < nodes.Count)
            {
                Type behaviourTreeType = m_behaviourTree.GetType();
                MethodInfo getChildrenMethod = behaviourTreeType.GetMethod("p_GetChildren", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                BehaviourTreeNode[] children = (BehaviourTreeNode[])getChildrenMethod.Invoke(m_behaviourTree, new object[] { nodes[index].id });

                foreach (BehaviourTreeNode child in children)
                {
                    nodes.Add(child.Copy());
                    parentIndices.Add(index);
                }

                index++;
            }

            return new NodeTemplate(nodes, parentIndices);
        }

        private class NodeTemplate
        {
            public List<BehaviourTreeNode> nodes;
            public List<int>               parentIndices;

            public NodeTemplate(IEnumerable<BehaviourTreeNode> nodes, IEnumerable<int> parentIndices)
            {
                this.nodes         = new List<BehaviourTreeNode>(nodes);
                this.parentIndices = new List<int>(parentIndices);
            }
        }
    }

    public static class BehaviourNodeTypeManager
    {
        private static Type[] s_behaviourNodeTypes;

        public static Type[] behaviourNodeTypes
        {
            get
            {
                if (s_behaviourNodeTypes == null)
                {
                    List<Type> res = new List<Type>();
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        res.AddRange(assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(BehaviourTreeNode)) && t.IsDefined(typeof(BehaviourNodeAttribute))));
                    }
                    s_behaviourNodeTypes = res.ToArray();
                }
                return s_behaviourNodeTypes;
            }
        }

        public static bool IsBehaviourNodeType(Type type)
        {
            return behaviourNodeTypes.Contains(type);
        }
    }

    public class BehaviourNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private List<SearchTreeEntry> m_searchEntries;
        private NodeTypeSelectCallback m_onSelect;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            p_Validate();
            return m_searchEntries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            m_onSelect?.Invoke((Type)entry.userData);
            return true;
        }


        public static BehaviourNodeSearchProvider Create(NodeTypeSelectCallback onSelect)
        {
            BehaviourNodeSearchProvider res = (BehaviourNodeSearchProvider)CreateInstance(typeof(BehaviourNodeSearchProvider));
            res.m_onSelect = onSelect;
            return res;
        }

        private BehaviourNodeSearchProvider()
        {
        }

        private void p_Validate()
        {
            if (true)
            {
                m_searchEntries = new List<SearchTreeEntry>();

                Type[] types = BehaviourNodeTypeManager.behaviourNodeTypes;

                m_searchEntries.Add(new SearchTreeGroupEntry(new GUIContent("Nodes"), 0));
                m_searchEntries.Add(new SearchTreeGroupEntry(new GUIContent("Composite"), 1));
                m_searchEntries.AddRange(types.Where(t => t.IsSubclassOf(typeof(CompositeNode))).Select(t => p_CreateEntry(t.Name, t)));
                m_searchEntries.Add(new SearchTreeGroupEntry(new GUIContent("Decorator"), 1));
                m_searchEntries.AddRange(types.Where(t => t.IsSubclassOf(typeof(DecoratorNode))).Select(t => p_CreateEntry(t.Name, t)));
                m_searchEntries.Add(new SearchTreeGroupEntry(new GUIContent("Action"), 1));
                m_searchEntries.AddRange(types.Where(t => t.IsSubclassOf(typeof(ActionNode))).Select(t => p_CreateEntry(t.Name, t)));
            }
        }

        private SearchTreeEntry p_CreateEntry(string name, Type type)
        {
            SearchTreeEntry entry = new SearchTreeEntry(new GUIContent(ObjectNames.NicifyVariableName(name).Replace(" Node", "")));
            entry.level = 2;
            entry.userData = type;
            return entry;
        }

        public delegate void NodeTypeSelectCallback(Type nodeType);
    }

}
