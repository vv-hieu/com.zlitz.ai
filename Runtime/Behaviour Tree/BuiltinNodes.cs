using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

using Zlitz.Utilities;

namespace Zlitz.AI 
{
    [Serializable]
    public abstract class WeightedPoolNode : CompositeNode, IBehaviourTreeNodeEventListener
    {
        [SerializeField]
        private SeedMode m_seedMode;

        private int m_seed;

        protected BehaviourTreeNode GetRandomChild(BehaviourTreeNode[] exclude)
        {
            Type behaviourTreeType = behaviourTree.GetType();
            MethodInfo getChildrenMethod = behaviourTreeType.GetMethod("p_GetChildren", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            BehaviourTreeNode[] children = (BehaviourTreeNode[])getChildrenMethod.Invoke(behaviourTree, new object[] { id });

            BehaviourTreeNode[] validChildren = children.Where(n => exclude == null ? true : !exclude.Contains(n)).ToArray();
            if (validChildren != null && validChildren.Length > 0)
            {
                RandomNumberGenerator rng = new RandomNumberGenerator(m_seed);
                float[] weights = validChildren.Select(n => p_NodeWeight(n)).ToArray();

                float totalWeight = 0.0f;
                int index = 0;
                foreach (BehaviourTreeNode child in validChildren)
                {
                    totalWeight += weights[index++];
                }

                float current = 0.0f;
                float random  = rng.NextFloat(0.0f, totalWeight);
                index = 0;
                foreach (BehaviourTreeNode child in validChildren)
                {
                    current += weights[index++];
                    if (current >= random)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        public void OnBehaviourTreeNodeStart(BehaviourTreeEvaluator evaluator)
        {
            m_seed = m_seedMode == SeedMode.PerEvaluator ? s_RandomSeed(evaluator.GetInstanceID()) : m_seedMode == SeedMode.RandomOnStart ? s_RandomSeed() : 0;
        }

        public void OnBehaviourTreeNodeEnd(BehaviourTreeEvaluator evaluator)
        {
        }

        private float p_NodeWeight(BehaviourTreeNode node)
        {
            if (node is IWeightedPoolEntryNode weightedNode)
            {
                return weightedNode.Weight();
            }
            return 1.0f;
        }

        private static int s_RandomSeed()
        {
            RandomNumberGenerator rng = new RandomNumberGenerator();

            int s1 = rng.NextInt(0, 256);
            int s2 = rng.NextInt(0, 256);
            int s3 = rng.NextInt(0, 256);
            int s4 = rng.NextInt(0, 256);

            return (((((s1 << 8) | s2) << 8) | s3) << 8) | s4;
        }
        
        private static int s_RandomSeed(int s)
        {
            RandomNumberGenerator rng = new RandomNumberGenerator(s);

            int s1 = rng.NextInt(0, 256);
            int s2 = rng.NextInt(0, 256);
            int s3 = rng.NextInt(0, 256);
            int s4 = rng.NextInt(0, 256);

            return (((((s1 << 8) | s2) << 8) | s3) << 8) | s4;
        }

        public enum SeedMode
        {
            PerEvaluator,
            RandomOnStart
        }
    }

    public interface IWeightedPoolEntryNode
    {
        public abstract float Weight();
    }

    // Composite nodes

    [Serializable, BehaviourNode]
    public class ParallelNode : CompositeNode
    {
        private bool   m_result;
        private bool[] m_complete;
        private int    m_incompleteCount;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (m_incompleteCount > 0)
            {
                int index = 0;
                foreach (BehaviourTreeNode child in children)
                {
                    if (!m_complete[index])
                    {
                        State result = child.Run(evaluator, context);
                        if (result != State.Running)
                        {
                            m_complete[index] = true;
                            m_result &= result == State.Success;
                            m_incompleteCount--;
                        }
                    }
                    index++;
                }
            }

            if (m_incompleteCount <= 0)
            {
                return m_result ? State.Success : State.Failure;
            }

            return State.Running;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_result          = true;
            m_complete        = new bool[children.Length];
            m_incompleteCount = children.Length;

            return true;
        }
    }

    [Serializable, BehaviourNode]
    public class SequenceNode : CompositeNode
    {
        private int m_currentChildIndex;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (children == null || m_currentChildIndex >= children.Length)
            {
                return State.Success;
            }

            State currentChildreState = children[m_currentChildIndex].Run(evaluator, context);
            if (currentChildreState == State.Failure)
            {
                return State.Failure;
            }
            if (currentChildreState == State.Success)
            {
                m_currentChildIndex++;
                if (m_currentChildIndex >= children.Length)
                {
                    return State.Success;
                }
            }
            return State.Running;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_currentChildIndex = 0;
            return true;
        }
    }

    [Serializable, BehaviourNode]
    public class SelectorNode : CompositeNode
    {
        private int m_currentChildIndex;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (children == null || m_currentChildIndex >= children.Length)
            {
                return State.Failure;
            }

            State currentChildreState = children[m_currentChildIndex].Run(evaluator, context);
            if (currentChildreState == State.Success)
            {
                return State.Success;
            }
            if (currentChildreState == State.Failure)
            {
                m_currentChildIndex++;
                if (m_currentChildIndex >= children.Length)
                {
                    return State.Failure;
                }
            }
            return State.Running;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_currentChildIndex = 0;
            return true;
        }
    }

    [Serializable, BehaviourNode]
    public class RandomSequenceNode : WeightedPoolNode
    {
        private int                     m_currentChildIndex;
        private List<BehaviourTreeNode> m_nodes;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (m_nodes == null || m_currentChildIndex >= m_nodes.Count)
            {
                return State.Success;
            }

            State currentChildreState = m_nodes[m_currentChildIndex].Run(evaluator, context);
            if (currentChildreState == State.Failure)
            {
                return State.Failure;
            }
            if (currentChildreState == State.Success)
            {
                m_currentChildIndex++;
                if (m_currentChildIndex >= m_nodes.Count)
                {
                    return State.Success;
                }
            }
            return State.Running;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_currentChildIndex = 0;

            if (m_nodes == null)
            {
                m_nodes = new List<BehaviourTreeNode>();
            }
            else
            {
                m_nodes.Clear();
            }

            for (int i = 0; i < children.Length; i++)
            {
                BehaviourTreeNode selectedChild = GetRandomChild(m_nodes.ToArray());
                m_nodes.Add(selectedChild);
            }

            return true;
        }
    }
    
    [Serializable, BehaviourNode]
    public class RandomSelectorNode : WeightedPoolNode
    {
        private int                     m_currentChildIndex;
        private List<BehaviourTreeNode> m_nodes;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (m_nodes == null || m_currentChildIndex >= m_nodes.Count)
            {
                return State.Failure;
            }

            State currentChildreState = m_nodes[m_currentChildIndex].Run(evaluator, context);
            if (currentChildreState == State.Success)
            {
                return State.Success;
            }
            if (currentChildreState == State.Failure)
            {
                m_currentChildIndex++;
                if (m_currentChildIndex >= m_nodes.Count)
                {
                    return State.Failure;
                }
            }
            return State.Running;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_currentChildIndex = 0;
            if (m_nodes == null)
            {
                m_nodes = new List<BehaviourTreeNode>();
            }
            else
            {
                m_nodes.Clear();
            }

            for (int i = 0; i < children.Length; i++)
            {
                BehaviourTreeNode selectedChild = GetRandomChild(m_nodes.ToArray());
                m_nodes.Add(selectedChild);
            }

            return true;
        }
    }

    // Decorator nodes

    [Serializable, BehaviourNode]
    public class ConditionNode : DecoratorNode
    {
        [SerializeField]
        private bool m_enabled;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (!m_enabled)
            {
                return State.Failure;
            }
            return child.Run(evaluator, context);
        }
    }

    [Serializable, BehaviourNode]
    public class ConditionFunctionNode : DecoratorNode
    {
        [SerializeField]
        private string m_conditionId;

        [SerializeField, HideInInspector]
        private string m_oldConditionId;

        private BehaviourPredicate m_condition;

        private BehaviourPredicate condition
        {
            get
            {
                if (m_condition == null || m_conditionId != m_oldConditionId)
                {
                    m_oldConditionId = m_conditionId;
                    m_condition = new BehaviourPredicate(m_conditionId);
                }
                return m_condition;
            }
        }

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (!condition.Check(evaluator))
            {
                return State.Failure;
            }
            return child.Run(evaluator, context);
        }
    }

    [Serializable, BehaviourNode]
    public class CallbackFunctionNode : DecoratorNode
    {
        [SerializeField]
        private string m_callbackId;

        [SerializeField, HideInInspector]
        private string m_oldCallbackId;

        private BehaviourActionResultCallback m_callback;

        private BehaviourActionResultCallback callback
        {
            get
            {
                if (m_callback == null || m_callbackId != m_oldCallbackId)
                {
                    m_oldCallbackId = m_callbackId;
                    m_callback = new BehaviourActionResultCallback(m_callbackId);
                }
                return m_callback;
            }
        }

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            State result = child.Run(evaluator, context);
            callback.Invoke(evaluator, result);
            return result;
        }
    }

    [Serializable, BehaviourNode]
    public class WeightNode : DecoratorNode, IWeightedPoolEntryNode
    {
        [SerializeField]
        private float m_weight = 1.0f;

        public float Weight()
        {
            return Mathf.Max(0.0f, m_weight);
        }

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            return child.Run(evaluator, context);
        }
    }

    [Serializable, BehaviourNode]
    public class InverseNode : DecoratorNode
    {
        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            State result = child.Run(evaluator, context);
            if (result != State.Running)
            {
                if (result == State.Success)
                {
                    return State.Failure;
                }
                if (result == State.Failure)
                {
                    return State.Success;
                }
            }
            return State.Running;
        }
    }

    [Serializable, BehaviourNode]
    public class AlwaysSucceedNode : DecoratorNode
    {
        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (child.Run(evaluator, context) != State.Running)
            {
                return State.Success;
            }
            return State.Running;
        }
    }

    [Serializable, BehaviourNode]
    public class AlwaysFailNode : DecoratorNode
    {
        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (child.Run(evaluator, context) != State.Running)
            {
                return State.Failure;
            }
            return State.Running;
        }
    }

    [Serializable, BehaviourNode]
    public class RepeatInfiniteNode : DecoratorNode
    {
        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (child != null)
            {
                if (child.Run(evaluator, context) == State.Failure)
                {
                    return State.Failure;
                }
                return State.Running;
            }
            return State.Failure;
        }
    }

    [Serializable, BehaviourNode]
    public class RepeatFixedNode : DecoratorNode
    {
        [SerializeField]
        private int m_iterations;

        private int m_current;

        public int iterations
        {
            get => m_iterations;
            set => m_iterations = value;
        }

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (child != null)
            {
                if (child.Run(evaluator, context) == State.Failure)
                {
                    return State.Failure;
                }
                m_current++;
                if (m_current >= m_iterations)
                {
                    return State.Success;
                }
                return State.Running;
            }
            return State.Failure;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_current = 0;
            return true;
        }
    }

    [Serializable, BehaviourNode]
    public class RepeatUntilNode : DecoratorNode
    {
        [SerializeField]
        private Condition m_until;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (child != null)
            {
                State result = child.Run(evaluator, context);
                if (result != State.Running && (
                    (m_until == Condition.Complete) || 
                    (m_until == Condition.Succeed && result == State.Success) || 
                    (m_until == Condition.Fail && result == State.Failure)))
                {
                    return State.Success;
                }
                return State.Running;
            }
            return State.Failure;
        }

        private enum Condition
        {
            Complete,
            Succeed,
            Fail
        }
    }
    
    // Action nodes

    [Serializable, BehaviourNode]
    public class EmptyNode : ActionNode
    {
    }

    [Serializable, BehaviourNode]
    public class WaitNode : ActionNode
    {
        [SerializeField]
        private float m_duration = 1.0f;

        private float m_time = 0.0f;

        public float duration
        {
            get => m_duration;
            set => m_duration = value;
        }

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            m_time += context.dt;
            if (m_time >= m_duration)
            {
                return State.Success;
            }
            return State.Running;
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            m_time = 0.0f;
            return true;
        }
    }

    [Serializable, BehaviourNode]
    public class ActionFunctionNode : ActionNode
    {
        [SerializeField]
        private string m_actionId;

        [SerializeField, HideInInspector]
        private string m_oldActionId;

        private BehaviourActionEvaluateCallback  m_evaluate;
        private BehaviourActionStartCallback     m_start;
        private BehaviourActionEndCallback       m_end;
        private BehaviourActionForceStopCallback m_forceStop;

        private BehaviourActionEvaluateCallback evaluateCallback
        {
            get
            {
                p_Validate();
                return m_evaluate;
            }
        }

        private BehaviourActionStartCallback startCallbcak
        {
            get
            {
                p_Validate();
                return m_start;
            }
        }

        private BehaviourActionEndCallback endCallback
        {
            get
            {
                p_Validate();
                return m_end;
            }
        }

        private BehaviourActionForceStopCallback forceStopCallback
        {
            get
            {
                p_Validate();
                return m_forceStop;
            }
        }

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            return evaluateCallback.Evaluate(evaluator, context);
        }

        protected override bool Start(BehaviourTreeEvaluator evaluator)
        {
            return startCallbcak.Start(evaluator);
        }

        protected override void End(BehaviourTreeEvaluator evaluator)
        {
            endCallback.End(evaluator);
        }

        protected override void ForceStop(BehaviourTreeEvaluator evaluator)
        {
            forceStopCallback.ForceStop(evaluator);
        }

        private void p_Validate()
        {
            if (m_oldActionId != m_actionId || m_evaluate == null)
            {
                m_evaluate = new BehaviourActionEvaluateCallback(m_actionId);
            }
            if (m_oldActionId != m_actionId || m_start == null)
            {
                m_start = new BehaviourActionStartCallback(m_actionId);
            }
            if (m_oldActionId != m_actionId || m_end == null)
            {
                m_end = new BehaviourActionEndCallback(m_actionId);
            }
            if (m_oldActionId != m_actionId || m_forceStop == null)
            {
                m_forceStop = new BehaviourActionForceStopCallback(m_actionId);
            }
            m_oldActionId = m_actionId;
        }
    }

    [Serializable, BehaviourNode]
    public class SubtreeNode : ActionNode
    {
        [SerializeField]
        private BehaviourTreeRunner m_subtree;

        protected override State Evaluate(BehaviourTreeEvaluator evaluator, BehaviourTree.RunContext context)
        {
            if (m_subtree != null)
            {
                return m_subtree.Run(evaluator, context);
            }
            return State.Failure;
        }

        protected override void ForceStop(BehaviourTreeEvaluator evaluator)
        {
            m_subtree?.ForceStop(evaluator);
        }
    }
}
