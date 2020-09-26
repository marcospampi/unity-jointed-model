using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace JointedModel {
    [Serializable]
    public class JMSNode {
        public int  parent_index,
                    first_child,
                    next_sibling;
        public string name;
        public Vector3 position;
        public Quaternion rotation;
    }

    public class JMSMarker {
        public string name, permutation;
        public int region, parent;
        public Quaternion rotation;
        public Vector3 position;
        public float radius;
    }


    public class JMSMaterial {
        public string name, path;
    }
    
    public class JMSVertex {
        public int node0, node1;
        public float node1weight;
        public Vector3
            position,
            normal,
            binormal,
            tangent;
        public Vector2 uv;
    }
    
    public class JMSTriangle {
        public int region, shader;
        public int[] vertices;
    }
    [Serializable]
    public class JMANodeState {
        public Vector3 position;
        public Quaternion rotation;
        public float scale;
    }

    public static class Constants {
        public static Regex
            JMS_V1_SPLIT_REGX = new Regex(@"\n+\s*|\t+",
                RegexOptions.Compiled | RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
        public const float FOOT = 3.048f;
        public const float JMX_SCALE = 100f;
        public const float RESCALE_MULTIPLY = FOOT / JMX_SCALE;
        public const int HALO_JMS_VERSION = 8200;
        
        public const string NODE_PREFIX = "@";
        public const string MARKER_PREFIX = "#";
        public static Matrix4x4 ROTATION_FIX = Matrix4x4.Rotate(
            Quaternion.Euler(-90,-90,0)
        );
    }
    public static class Utils {
        // https://answers.unity.com/questions/402280/how-to-decompose-a-trs-matrix.html
        public static Vector3 FlipX ( Vector3 v ) {
            return new Vector3( -v.x,v.y,v.z);
        }
        public static Vector3 FlipY ( Vector3 v ) {
            return new Vector3( v.x,-v.y,v.z);
        }
        public static Vector3 ExtractPosition( Matrix4x4 m ) {
            return m.GetColumn(3);
        }
        public static Quaternion ExtractRotation ( Matrix4x4 m ) {
            return Quaternion.LookRotation(
                m.GetColumn(2),
                m.GetColumn(1)
            );
        }
        public static Vector3 ExtractScale ( Matrix4x4 m ) {
            return new Vector3(
                m.GetColumn(0).magnitude,
                m.GetColumn(1).magnitude,
                m.GetColumn(2).magnitude
            );
        }
        public static (Vector3,Quaternion,Vector3) DecomposeMatrix ( Matrix4x4 m ) {
            return ( ExtractPosition(m),ExtractRotation(m),ExtractScale(m));
        }
    }

}