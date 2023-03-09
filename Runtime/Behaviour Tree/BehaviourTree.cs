using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Zlitz.AI
{
    [CreateAssetMenu(menuName = "Zlitz/AI/Behaviour Tree")]
    public class BehaviourTree : ScriptableObject
    {
        [SerializeReference]
        private List<BehaviourTreeNode> m_nodes;

        [SerializeField]
        private int m_rootId = 0;

        [SerializeField]
        private List<int> m_parentIndices;

        [SerializeField]
        private List<Property> m_properties;

        [SerializeField]
        private bool m_init = false;

        [SerializeField]
        private string m_version;

        public BehaviourTreeNode[] nodes => m_nodes.ToArray();

        public BehaviourTreeNode root => m_nodes[m_rootId];

        public string version => m_version;

        public BehaviourTree Instantiate()
        {
            BehaviourTree res = Instantiate(this);
            res.p_UpdateNodes();
            return res;
        }

        public BehaviourTreeNode.State Run(BehaviourTreeEvaluator evaluator, RunContext context)
        {
            if (root == null)
            {
                return BehaviourTreeNode.State.Failure;
            }
            return root.Run(evaluator, context);
        }

        public void ForceStop(BehaviourTreeEvaluator evaluator)
        {
            List<BehaviourTreeNode> nodes = new List<BehaviourTreeNode>();
            if (root.started)
            {
                nodes.Add(root);
            }

            while (nodes.Count > 0)
            {
                BehaviourTreeNode node = nodes[0];
                nodes.RemoveAt(0);

                nodes.AddRange(p_GetChildren(node.id).Where(n => n.started));
                node.p_ForceStop(evaluator);
            }
        }

        private void Reset()
        {
            if (m_init)
            {
                return;
            }
            m_init = true;

            m_nodes         = new List<BehaviourTreeNode>();
            m_parentIndices = new List<int>();
            m_properties    = new List<Property>();

            RepeatInfiniteNode root = new RepeatInfiniteNode();
            root.name = "Root";

            m_version = Guid.NewGuid().ToString();

            p_Add(root);
        }

        private void p_UpdateNodes()
        {
            foreach (BehaviourTreeNode node in m_nodes)
            {
                if (node == null)
                {
                    continue;
                }
                node.p_SetBehaviourTree(this);
            }
        }

        private void p_Add(BehaviourTreeNode node, BehaviourTreeNode parent = null)
        {
            int parentId = parent == null ? -1 : parent.id;
            int lastSiblingIdx = -1;
            for (int i = 0; i < m_nodes.Count; i++)
            {
                if (m_parentIndices[i] == parentId)
                {
                    lastSiblingIdx = i;
                }
            }
            for (int i = lastSiblingIdx + 1; i < m_nodes.Count; i++)
            {
                if (m_nodes[i] == null)
                {
                    node.id = i;

                    m_nodes[i]         = node;
                    m_parentIndices[i] = parentId;

                    node.Initialize(this);
                    return;
                }
            }
            node.id = m_nodes.Count;
            m_nodes.Add(node);
            m_parentIndices.Add(parentId);

            node.Initialize(this);
        }

        private void p_AddParent(BehaviourTreeNode node, BehaviourTreeNode newParent)
        {
            int currentId = node.id;

            bool found = false;
            for (int i = 0; i < m_nodes.Count; i++)
            {
                if (m_nodes[i] == null)
                {
                    foreach (BehaviourTreeNode child in p_GetChildren(currentId))
                    {
                        m_parentIndices[child.id] = i;
                    }

                    m_nodes[i] = node;
                    m_parentIndices[i] = currentId;
                    node.id = i;
                    found = true;

                    break;
                }
            }
            if (!found)
            {
                int newId = m_nodes.Count;

                foreach (BehaviourTreeNode child in p_GetChildren(currentId))
                {
                    m_parentIndices[child.id] = newId;
                }

                m_nodes.Add(node);
                m_parentIndices.Add(currentId);
                node.id = newId;
            }

            m_nodes[currentId] = newParent;
            newParent.Initialize(this);
            newParent.id = currentId;
        }

        private void p_Remove(int id)
        {
            if (id <= 0 || id >= m_nodes.Count)
            {
                return;
            }

            List<int> removedIds = new List<int>();
            removedIds.Add(id);

            while (removedIds.Count > 0)
            {
                id = removedIds[0];
                removedIds.RemoveAt(0);

                for (int i = 0; i < m_nodes.Count; i++)
                {
                    if (m_parentIndices[i] == id)
                    {
                        removedIds.Add(i);
                    }
                }
                m_nodes[id]         = null;
                m_parentIndices[id] = -1;

                m_properties = m_properties.Where(p => p.nodeId != id).ToList();
            }
        }

        private void p_Replace(int id, BehaviourTreeNode node)
        {
            if (id < 0 || id >= m_nodes.Count)
            {
                return;
            }

            int keepChildren = node.isComposite ? p_GetChildren(id).Length : node.isDecorator ? 1 : 0;

            List<int> removedIds = new List<int>();
            removedIds.Add(id);

            bool first = true;
            while (removedIds.Count > 0)
            {
                int nextId = removedIds[0];
                removedIds.RemoveAt(0);

                for (int i = 0; i < m_nodes.Count; i++)
                {
                    if (first)
                    {
                        if (m_parentIndices[i] == nextId)
                        {
                            if (keepChildren <= 0)
                            {
                                removedIds.Add(i);
                            }
                            keepChildren--;
                        }
                    }
                    else
                    {
                        if (m_parentIndices[i] == nextId)
                        {
                            removedIds.Add(i);
                        }
                    }
                    
                    if (nextId != id)
                    {
                        m_nodes[nextId] = null;
                        m_parentIndices[nextId] = -1;

                        m_properties = m_properties.Where(p => p.nodeId != nextId).ToList();
                    }
                }
                first = false;
            }

            node.id = id;
            m_nodes[id] = node;
            node.Initialize(this);
        }

        private void p_MoveLeft(int id)
        {
            if (id < 0 || id >= m_nodes.Count)
            {
                return;
            }

            int[] childIds = p_GetChildren(id).Select(n => n.id).ToArray();
            for (int i = id - 1; i >= 0; i--)
            {
                if (m_parentIndices[i] == m_parentIndices[id])
                {
                    int[] childIds2 = p_GetChildren(i).Select(n => n.id).ToArray();
                    foreach (int childId in childIds)
                    {
                        m_parentIndices[childId] = i;
                    }
                    foreach (int childId2 in childIds2)
                    {
                        m_parentIndices[childId2] = id;
                    }

                    BehaviourTreeNode temp = m_nodes[i];
                    m_nodes[i].id = id;
                    m_nodes[id].id = i;

                    m_nodes[i] = m_nodes[id];
                    m_nodes[id] = temp;

                    return;
                }
            }
        }

        private void p_MoveRight(int id)
        {
            if (id < 0 || id >= m_nodes.Count)
            {
                return;
            }

            int[] childIds = p_GetChildren(id).Select(n => n.id).ToArray();
            for (int i = id + 1; i < m_nodes.Count; i++)
            {
                if (m_parentIndices[i] == m_parentIndices[id])
                {
                    int[] childIds2 = p_GetChildren(i).Select(n => n.id).ToArray();
                    foreach (int childId in childIds)
                    {
                        m_parentIndices[childId] = i;
                    }
                    foreach (int childId2 in childIds2)
                    {
                        m_parentIndices[childId2] = id;
                    }

                    BehaviourTreeNode temp = m_nodes[i];
                    m_nodes[i].id = id;
                    m_nodes[id].id = i;

                    m_nodes[i] = m_nodes[id];
                    m_nodes[id] = temp;

                    return;
                }
            }
        }

        private bool p_IsDescendantOf(int id, int ancestorId)
        {
            while (true)
            {
                if (id == -1)
                {
                    return false;
                }
                if (id == ancestorId)
                {
                    return true;
                }
                id = m_parentIndices[id];
            }
        }

        internal BehaviourTreeNode[] p_GetChildren(int parentId)
        {
            List<BehaviourTreeNode> res = new List<BehaviourTreeNode>();
            for (int i = 0; i < m_nodes.Count; i++)
            {
                if (m_parentIndices[i] == parentId && m_nodes[i] != null)
                {
                    res.Add(m_nodes[i]);
                }
            }
            return res.ToArray();
        }

        internal BehaviourTreeNode p_GetParent(int id)
        {
            if (id < 0 || id >= m_parentIndices.Count || m_parentIndices[id] < 0 || m_parentIndices[id] >= m_nodes.Count)
            {
                return null;
            }
            return m_nodes[m_parentIndices[id]];
        }

        [Serializable]
        public struct Property
        {
            public string displayName;
            public int    nodeId;
            public string propertyName;
        }
        public class RunContext
        {
            public float dt { get; private set; }

            public static RunContext Create()
            {
                return new RunContext();
            }

            public RunContext DeltaTime(float dt)
            {
                this.dt = dt;
                return this;
            }

            private RunContext()
            {
            }
        }
    }

    [Serializable]
    public abstract class BehaviourTreeNode
    {
        [SerializeField]
        private string m_name;

        [SerializeField]
        private int m_id;

        [SerializeField]
        private BehaviourTree m_behaviourTree;

        [SerializeField]
        private bool m_exposed = true;

        private bool m_started = false;

        private int m_nodeState;

        public BehaviourTreeNode parent => m_behaviourTree.p_GetParent(m_id);

        public bool exposed => m_exposed;

        internal bool started
        {
            get => m_started;
            set => m_started = value;
        }

        public string name
        {
            get => m_name;
            set => m_name = value;
        }

        public int id
        {
            get => m_id;
            internal set => m_id = value;
        }

        public int nodeState => m_nodeState;

        public BehaviourTree behaviourTree => m_behaviourTree;

        public bool isComposite => GetType().IsSubclassOf(typeof(CompositeNode));
        public bool isDecorator => GetType().IsSubclassOf(typeof(DecoratorNode));
        public bool isAction => GetType().IsSubclassOf(typeof(ActionNode));
        public bool isRoot => m_id == m_behaviourTree.root.id;

        public State Run(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (!m_started)
            {
                if (this is IBehaviourTreeNodeEventListener listener)
                {
                    listener.OnBehaviourTreeNodeStart(evaluator);
                }
                if (!Start(evaluator))
                {
                    m_nodeState = 2;
                    return State.Failure;
                }
                foreach (BehaviourTreeNode child in m_behaviourTree.p_GetChildren(m_id))
                {
                    child.p_ResetState();
                }
                m_started = true;
                m_nodeState = 0;
            }
            State res = Evaluate(evaluator, context);
            m_nodeState = res == State.Running ? 0 : res == State.Success ? 1 : 2;
            if (res != State.Running)
            {
                m_started = false;
                End(evaluator);
                foreach (BehaviourTreeNode child in m_behaviourTree.p_GetChildren(m_id))
                {
                    if (child.m_started)
                    {
                        child.m_started = false;
                        child.ForceStop(evaluator);
                    }
                }

                if (this is IBehaviourTreeNodeEventListener listener)
                {
                    listener.OnBehaviourTreeNodeEnd(evaluator);
                }
            }
            return res;
        }

        public BehaviourTreeNode Copy()
        {
            return (BehaviourTreeNode)MemberwiseClone();
        }

        public virtual void Initialize(BehaviourTree behaviourTree)
        {
            m_behaviourTree = behaviourTree;
        }

        protected virtual State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            return State.Success;
        }

        protected virtual bool Start(BehaviourTreeEvaluator evaluator)
        {
            return true;
        }

        protected virtual void End(BehaviourTreeEvaluator evaluator)
        {
        }

        protected virtual void ForceStop(BehaviourTreeEvaluator evaluator)
        {
        }

        internal void p_ForceStop(BehaviourTreeEvaluator evaluator)
        {
            ForceStop(evaluator);
        }

        internal void p_SetBehaviourTree(BehaviourTree behaviourTree)
        {
            m_behaviourTree = behaviourTree;
        }

        private void p_ResetState()
        {
            m_nodeState = -1;
            foreach (BehaviourTreeNode child in m_behaviourTree.p_GetChildren(m_id))
            {
                child.p_ResetState();
            }
        }

        public enum State
        {
            Running,
            Success,
            Failure
        }
    }

    [Serializable]
    public abstract class CompositeNode : BehaviourTreeNode
    {
        public BehaviourTreeNode[] children => behaviourTree.p_GetChildren(id);

        public BehaviourTreeNode Add(Type nodeType)
        {
            if (nodeType.IsSubclassOf(typeof(BehaviourTreeNode)))
            {
                BehaviourTreeNode newNode = (BehaviourTreeNode)Activator.CreateInstance(nodeType);
                newNode.Initialize(behaviourTree);

                Type type = behaviourTree.GetType();
                MethodInfo addMethod = type.GetMethod("p_Add", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                addMethod.Invoke(behaviourTree, new object[] { newNode, this });

                return newNode;
            }

            return null;
        }

        public T Add<T>() where T : BehaviourTreeNode
        {
            T newNode = Activator.CreateInstance<T>();
            newNode.Initialize(behaviourTree);

            Type type = behaviourTree.GetType();
            MethodInfo addMethod = type.GetMethod("p_Add", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            addMethod.Invoke(behaviourTree, new object[] { newNode, this });

            return newNode;
        }
    }

    [Serializable]
    public abstract class DecoratorNode : BehaviourTreeNode
    {
        public BehaviourTreeNode child => behaviourTree.p_GetChildren(id)[0];

        public override void Initialize(BehaviourTree behaviourTree)
        {
            base.Initialize(behaviourTree);
            if (behaviourTree.p_GetChildren(id) == null || behaviourTree.p_GetChildren(id).Length <= 0)
            {
                EmptyNode newNode = new EmptyNode();
                newNode.Initialize(behaviourTree);

                Type type = behaviourTree.GetType();
                MethodInfo addMethod = type.GetMethod("p_Add", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                addMethod.Invoke(behaviourTree, new object[] { newNode, this });
            }
        }
    }

    [Serializable]
    public abstract class ActionNode : BehaviourTreeNode
    {
    }

    public interface IBehaviourTreeNodeEventListener
    {
        void OnBehaviourTreeNodeStart(BehaviourTreeEvaluator evaluator);

        void OnBehaviourTreeNodeEnd(BehaviourTreeEvaluator evaluator);
    }
}
