using UnityEngine;

namespace DLS.Game
{
    public class ConfettiManager : MonoBehaviour
    {
        public ParticleSystem leftConfettiEffect;
        public ParticleSystem rightConfettiEffect;

        private void Awake()
        {
            // Store reference globally
            Main.LeftConfettiEffect = leftConfettiEffect;
            Main.RightConfettiEffect = rightConfettiEffect;
        }
    }
}
