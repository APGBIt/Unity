using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>Lista glosa → clip de animación humanoide (crear con Assets > Create > LSRD > Clips de señas).</summary>
    [CreateAssetMenu(menuName = "LSRD/Clips de señas", fileName = "ClipsDeSenas")]
    public sealed class ClipsDeSenas : ScriptableObject
    {
        [Serializable]
        public sealed class Par
        {
            public string Glosa;
            public AnimationClip Clip;
        }

        public List<Par> Clips = new List<Par>();
    }
}
