using UnityEngine;

namespace Zlitz.AI
{
    public class BehaviourTreeEvaluator : MonoBehaviour
    {
        [SerializeField]
        private BehaviourTreeRunner m_behaviourTree;

        public BehaviourTree behaviourTree
        {
            get => m_behaviourTree == null ? null : m_behaviourTree.blueprint;
            set
            {
                if (m_behaviourTree != null)
                {
                    m_behaviourTree.blueprint = value;
                }
            }
        }

        public T GetProperty<T>(string name)
        {
            if (m_behaviourTree != null && m_behaviourTree.TryGetProperty(name, out T value)) 
            {
                return value;
            }
            return default(T);
        }

        public void SetProperty<T>(string name, T value)
        {
            m_behaviourTree?.TrySetProperty(name, value);
        }

        private void Update()
        {
            m_behaviourTree?.Run(this, p_CreateContext());
        }

        private BehaviourTree.RunContext p_CreateContext()
        {
            return BehaviourTree.RunContext.Create()
                .DeltaTime(Time.deltaTime);
        }
    }
}
