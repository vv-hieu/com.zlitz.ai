using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace Zlitz.AI
{
    [Serializable]
    public class BehaviourTreeRunner : ISerializationCallbackReceiver
    {
        [SerializeField]
        private BehaviourTree m_blueprintTree;

        [SerializeField]
        private BehaviourTree m_instantiatedTree;

        public BehaviourTree blueprint
        {
            get => m_blueprintTree;
            set
            {
                m_blueprintTree = value;
                p_Validate();
            }
        }

        public bool TryGetProperty<T>(string name, out T value)
        {
            if (m_instantiatedTree != null)
            {
                Type behaviourTreeType = m_instantiatedTree.GetType();

                FieldInfo propertiesField = behaviourTreeType.GetField("m_properties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                List<BehaviourTree.Property> properties = (List<BehaviourTree.Property>)propertiesField.GetValue(m_instantiatedTree);

                foreach (BehaviourTree.Property property in properties)
                {
                    if (property.displayName == name)
                    {
                        FieldInfo nodesField = behaviourTreeType.GetField("m_nodes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        List<BehaviourTreeNode> nodes = (List<BehaviourTreeNode>)nodesField.GetValue(m_instantiatedTree);

                        BehaviourTreeNode node = nodes[property.nodeId];
                        Type nodeType = node.GetType();
                        while (nodeType != null)
                        {
                            FieldInfo propertyField = nodeType.GetField(property.propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (propertyField != null)
                            {
                                if (propertyField.FieldType == typeof(T))
                                {
                                    value = (T)propertyField.GetValue(node);
                                    return true;
                                }
                            }
                            nodeType = nodeType.BaseType;
                        }
                    }
                }
            }

            value = default(T);
            return false;
        }

        public bool TrySetProperty<T>(string name, T value)
        {
            if (m_instantiatedTree != null)
            {
                Type behaviourTreeType = m_instantiatedTree.GetType();

                FieldInfo propertiesField = behaviourTreeType.GetField("m_properties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                List<BehaviourTree.Property> properties = (List<BehaviourTree.Property>)propertiesField.GetValue(m_instantiatedTree);

                foreach (BehaviourTree.Property property in properties)
                {
                    if (property.displayName == name)
                    {
                        FieldInfo nodesField = behaviourTreeType.GetField("m_nodes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        List<BehaviourTreeNode> nodes = (List<BehaviourTreeNode>)nodesField.GetValue(m_instantiatedTree);
                        
                        BehaviourTreeNode node = nodes[property.nodeId];
                        Type nodeType = node.GetType();
                        while (nodeType != null)
                        {
                            FieldInfo propertyField = nodeType.GetField(property.propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (propertyField != null)
                            {
                                if (propertyField.FieldType == typeof(T))
                                {
                                    propertyField.SetValue(node, value);
                                    return true;
                                }
                            }
                            nodeType = nodeType.BaseType;
                        }
                    }
                }
            }

            return false;
        }

        public BehaviourTreeNode.State Run(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            p_Validate();
            return p_Run(evaluator, context);
        }

        public void ForceStop(BehaviourTreeEvaluator evaluator)
        {
            m_instantiatedTree?.ForceStop(evaluator);
        }

        private void p_Validate()
        {
            if (m_blueprintTree != null && (m_instantiatedTree == null || m_instantiatedTree.version != m_blueprintTree.version))
            {
                if (m_instantiatedTree != null)
                {
                    ScriptableObject.DestroyImmediate(m_instantiatedTree);
                }
                m_instantiatedTree = m_blueprintTree.Instantiate();
            }
            else if (m_blueprintTree == null)
            {
                if (m_instantiatedTree != null)
                {
                    ScriptableObject.DestroyImmediate(m_instantiatedTree);
                }
                m_instantiatedTree = null;
            }
        }

        private BehaviourTreeNode.State p_Run(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (m_instantiatedTree != null)
            {
                return m_instantiatedTree.Run(evaluator, context);
            }
            return BehaviourTreeNode.State.Failure;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() => p_Validate();
        void ISerializationCallbackReceiver.OnAfterDeserialize() { }
    }
}
