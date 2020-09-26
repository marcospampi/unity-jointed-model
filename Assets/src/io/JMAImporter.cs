using System;
using CultureInfo = System.Globalization.CultureInfo;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor;

namespace JointedModel {

  [ScriptedImporter(4, new string[] { "jma","jmm","jmt","jmo","jmr","jmrx","jmz","jmw" })]
  public class JMAImporter : ScriptedImporter {

    public enum HowDoIHandleThis {
      SingleAnimation,
      OneFrameOneAnim,
      Default
    }
    public HowDoIHandleThis anim_handle_mode = HowDoIHandleThis.Default;
    public bool loopTime = false;
    private int version;
    private int frame_count;
    private int frame_rate;
    private string actor;
    
    private JMSNode[] nodes;

    private JMANodeState[,] keyframes;


    public override void OnImportAsset(AssetImportContext ctx)
    {
      string name = Path.GetFileNameWithoutExtension(ctx.assetPath);
      string extension = Path.GetExtension(ctx.assetPath);

      this.Populate(ReadFile(ctx.assetPath));
      this.DoParenting();
      //this.PreprocessAnimation();
      List<AnimationClip> clips;

      if ( this.anim_handle_mode == HowDoIHandleThis.Default && extension == ".jmo" ) 
        clips = this.GenerateClips( true );
      
      else 
        clips = this.GenerateClips( this.anim_handle_mode == HowDoIHandleThis.OneFrameOneAnim );
      
      if ( clips.Count == 1 ) {
        clips[0].name = name;
      }
      else {
        for ( int i = 0, I = clips.Count; i < I; i++ ) {
          clips[i].name = name + "." + string.Format("{0:00}",i);
        }
      }
      foreach ( var clip in clips ){
        if ( this.loopTime ) {
          AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
          settings.loopTime = true;
          AnimationUtility.SetAnimationClipSettings(clip,settings);
        }
        ctx.AddObjectToAsset(clip.name,clip);
      }
      
      
    }
    private string[] ReadFile( string filePath ) {
      string text = File.ReadAllText( filePath );
      return Constants.JMS_V1_SPLIT_REGX.Split( text );
    }
    public void Populate( string[] text ) {
      
      int i = 0;

      Func<float> readFloat = () => float.Parse(text[i++],CultureInfo.InvariantCulture);
      Func<int> readInt = () => int.Parse(text[i++], CultureInfo.InvariantCulture);
      Func<string> readString = () => text[i++];

      this.version = readInt();
      if ( this.version != 16392 )
        throw new NotSupportedException(string.Format("Unsupported {0} version",this.version));
      
      this.frame_count = readInt();
      this.frame_rate = readInt();

      {
        int actors;
        if ( (actors = readInt()) != 1 )
          throw new NotSupportedException(string.Format("Only one actor is supported ATM. {0} provided",actors));
      }
      this.actor = readString();

      this.nodes = new JMSNode[readInt()];
      /* checksum = */ readInt();

      for ( int n = 0, N = this.nodes.Length; n < N; n++ ) {
        var node = new JMSNode();

        node.name = readString();
        node.first_child = readInt();
        node.next_sibling = readInt();
        node.parent_index = -1;

        this.nodes[n] = node;
      }

      this.keyframes = new JMANodeState[this.frame_count,this.nodes.Length];

      for ( int f = 0, F = frame_count; f < F; f++ ) {
        for ( int n = 0, N = this.nodes.Length; n < N; n++ ) {

          JMANodeState state = new JMANodeState();

          state.position = new Vector3(
            readFloat(), readFloat(), readFloat()
          );

          state.rotation = new Quaternion(
            readFloat(), readFloat(), readFloat(), readFloat()
          );
          state.scale = readFloat();

          this.keyframes[f,n] = state;

        }
      }
    }
    private void DoParenting() {
      for ( int n = 0, N = this.nodes.Length; n < N; n++ ) {
          int child = this.nodes[n].first_child;
          int iters = 0;
          while ( child != -1 && iters < 32) {
            
            this.nodes[child].parent_index = n;

            child = this.nodes[child].next_sibling;
            iters++;
          }
        }
      
    }
    private string GetNodePath( int n ) {
      List<string> path = new List<string>();

      int iters = 0;
      while ( n != -1 && iters < 32 ) {
        path.Add( Constants.NODE_PREFIX + this.nodes[n].name );
        n = this.nodes[n].parent_index;
        iters++;
      }

      return string.Join("/",path.ToArray().Reverse());
    }
    
    private List<AnimationClip> GenerateClips( bool split ) {
      List<AnimationClip> clips = new List<AnimationClip>();

    
      if ( split ) {

        for( int f = 0, F = this.frame_count; f < F; f++ ) {
          var clip = new AnimationClip();
          clip.frameRate = 1;

          for ( int n = 0, N = this.nodes.Length; n < N; n++ ) {


            var position = this.keyframes[f,n].position;
            var rotation = this.keyframes[f,n].rotation;
              

            var translation_frame =  new Keyframe[] { 
                new Keyframe(0,position.x * Constants.RESCALE_MULTIPLY),
                new Keyframe(0,position.z * Constants.RESCALE_MULTIPLY),
                new Keyframe(0,position.y * Constants.RESCALE_MULTIPLY)
            };

            var rotation_frame = new Keyframe[] { 
                new Keyframe(0,rotation.x),
                new Keyframe(0,rotation.z),
                new Keyframe(0,rotation.y),
                new Keyframe(0,rotation.w)
            };

            string relativePath = GetNodePath(n);

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve( new Keyframe[] {translation_frame[0]} )
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localPosition.y",
                new AnimationCurve( new Keyframe[] {translation_frame[1]} )
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localPosition.z",
                new AnimationCurve(  new Keyframe[] {translation_frame[2]}  )
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localRotation.x",
                new AnimationCurve( new Keyframe[] {rotation_frame[0]}  )
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localRotation.y",
                new AnimationCurve( new Keyframe[] {rotation_frame[1]} )
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localRotation.z",
                new AnimationCurve( new Keyframe[] {rotation_frame[2]} )
            );

            clip.SetCurve(
                relativePath,
                typeof(Transform),
                "localRotation.w",
                new AnimationCurve( new Keyframe[] {rotation_frame[3]} )
            );

          }
          clips.Add(clip);
        }

      }
      else {
        var clip = new AnimationClip();
        clip.frameRate = this.frame_rate;
        for ( int n = 0, N = this.nodes.Length; n < N; n++ ) {

          Vector3[] positions = new Vector3[this.frame_count];
          Quaternion[] rotations = new Quaternion[this.frame_count];

          for ( int f = 0, F = this.frame_count; f < F; f++ ) {
            positions[f] = this.keyframes[f,n].position;
            rotations[f] = this.keyframes[f,n].rotation;
          }

          var translation_frames = positions.AsEnumerable().Select((e,t) => new Keyframe[] { 
            new Keyframe((float)t/this.frame_count,e.x * Constants.RESCALE_MULTIPLY),
            new Keyframe((float)t/this.frame_count,e.z * Constants.RESCALE_MULTIPLY),
            new Keyframe((float)t/this.frame_count,e.y * Constants.RESCALE_MULTIPLY)
          });

          var rotation_frames = rotations.AsEnumerable().Select((e,t) => new Keyframe[] { 
            new Keyframe((float)t/this.frame_count,e.x),
            new Keyframe((float)t/this.frame_count,e.z),
            new Keyframe((float)t/this.frame_count,e.y),
            new Keyframe((float)t/this.frame_count,e.w)
          });

          string relativePath = GetNodePath(n);

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localPosition.x",
            new AnimationCurve( translation_frames.Select(e => e[0]).ToArray() )
          );

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localPosition.y",
            new AnimationCurve( translation_frames.Select(e => e[1]).ToArray() )
          );

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localPosition.z",
            new AnimationCurve( translation_frames.Select(e => e[2]).ToArray() )
          );

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.x",
            new AnimationCurve( rotation_frames.Select(e => e[0]).ToArray() )
          );

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.y",
            new AnimationCurve( rotation_frames.Select(e => e[1]).ToArray() )
          );

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.z",
            new AnimationCurve( rotation_frames.Select(e => e[2]).ToArray() )
          );

          clip.SetCurve(
            relativePath,
            typeof(Transform),
            "localRotation.w",
            new AnimationCurve( rotation_frames.Select(e => e[3]).ToArray() )
          );

        }
        clips.Add(clip);
      }
     
      return clips;
    }
  }

}
