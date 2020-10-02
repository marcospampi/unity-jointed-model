using System;
using CultureInfo = System.Globalization.CultureInfo;

using System.Linq;
using System.IO;

using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

namespace JointedModel
{
  [ScriptedImporter(3, "jms")]
  public class JMSImporter : ScriptedImporter
  {
		// todo: public bool SeparateShaders = false;
		private int version;
		private int checksum;
    private JMSNode[] nodes;
		private JMSMaterial[] materials;
		private JMSMarker[] markers;
		private string[] regions;
		private JMSVertex[] vertices;
		private JMSTriangle[] triangles;
		// holy cow
		private Matrix4x4[] nodes_matrix;

    public override void OnImportAsset( AssetImportContext ctx ) {
      string[] text = ReadFile(ctx.assetPath);
			
			this.Populate( text );

			var root = new GameObject();
			root.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
			GameObject[] nodes = this.GenerateNodes();
			this.GenerateMarkers(nodes);

			nodes[0].transform.parent = root.transform;

			Material[] materials = this.materials.Select(
				m => {
					var material = new Material(Shader.Find("Diffuse"));
					material.name = m.name;
					return material;
				}
			).ToArray();

			Mesh[] meshes = this.GenerateMeshesh();

			GameObject[] models = meshes.Select(
				mesh => {
					var model = new GameObject(mesh.name);
					
					MeshFilter meshFilter = model.AddComponent<MeshFilter>();
					SkinnedMeshRenderer skinnedMeshRenderer = model.AddComponent<SkinnedMeshRenderer>();

					meshFilter.sharedMesh = mesh;
					skinnedMeshRenderer.sharedMesh = mesh;
				
					skinnedMeshRenderer.quality = SkinQuality.Bone2;

					mesh.bindposes = nodes.Select(
						node => node.transform.worldToLocalMatrix * model.transform.localToWorldMatrix
					).ToArray();

					skinnedMeshRenderer.bones = nodes.Select(e => e.transform).ToArray();
					skinnedMeshRenderer.rootBone = nodes[0].transform;

					skinnedMeshRenderer.sharedMaterials = materials;

					model.transform.parent = root.transform;

					return model;
				}
			).ToArray();

			foreach( Material mat in materials )
				ctx.AddObjectToAsset(mat.name,mat);

			foreach( Mesh mesh in meshes )
				ctx.AddObjectToAsset( mesh.name, mesh );


			// avatar
			Avatar avatar = AvatarBuilder.BuildGenericAvatar(root,nodes[0].transform.name);
			avatar.name = root.name+"Avatar";

			avatar.hideFlags = HideFlags.None;
						
			Animator animator = root.AddComponent<Animator>();
			animator.avatar = avatar;

			AvatarMask mask = new AvatarMask();

			mask.name = root.name + "AvatarMask";
			foreach( var node in nodes ) {
				mask.AddTransformPath(node.transform);
			}

			ctx.AddObjectToAsset(avatar.name, avatar);
			ctx.AddObjectToAsset(mask.name,mask);
			ctx.AddObjectToAsset("root",root);

			ctx.SetMainObject(root);

			
    }

		/// <summary>
		/// 	Open a JMS file specified by filePath,
		/// 	then read its content and split it by JMS_V1_SPLIT_REGX
		/// </summary>
		/// <param name="filePath"> File to open </param>
		/// <returns></returns>
    private string[] ReadFile( string filePath ) {
      string text = File.ReadAllText( filePath );
      return Constants.JMS_V1_SPLIT_REGX.Split( text );
    }

		/// <summary> Populates this class private stuff </summary>
		/// <param name="text"> Splitted text representation of a JMS file </param>
		private void Populate ( string[] text ) {
			int i = 0;
			
			this.version = int.Parse(text[i++]);
			if( this.version != Constants.HALO_JMS_VERSION ) {
				throw new NotSupportedException(
					string.Format( "Unsupported JMS version {0}" ,this.version )
				);
			}

			this.checksum = int.Parse( text[i++] );
			
			// parsing nodes
			this.nodes = new JMSNode[ int.Parse( text[i++] ) ];

			for ( int n = 0, N = this.nodes.Length; n < N; n++ ) {
				JMSNode node = new JMSNode();

				node.name = text[i++];

				node.parent_index = -1; 
				node.first_child = int.Parse( text[i++] );
				node.next_sibling = int.Parse( text[i++] );
				
				
				node.rotation = new Quaternion(
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture )
				);

				node.position = new Vector3(
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture )
				);

				this.nodes[n] = node;

			}

			// parsing materials 
			this.materials = new JMSMaterial[ int.Parse( text[i++] ) ];

			for ( int m = 0, M = this.materials.Length; m < M; m++ ) {
				
				JMSMaterial material = new JMSMaterial();

				material.name = text[i++];
				material.path = text[i++];

				this.materials[m] = material;

			}

			// parse markers
			this.markers = new JMSMarker[ int.Parse( text[i++] ) ];

			for ( int m = 0, M = this.markers.Length; m < M; m++ ) {

				JMSMarker marker = new JMSMarker();

				marker.name = text[i++];
				marker.permutation = "unnamed";


				marker.region = int.Parse(text[i++]);
				marker.parent = int.Parse(text[i++]);

				marker.rotation = new Quaternion(
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture )
				);

				marker.position = new Vector3(
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture )
				);

				marker.radius = float.Parse( text[i++], CultureInfo.InvariantCulture );
				this.markers[m] = marker;

			}

			// parse regions
			this.regions = new string[ int.Parse( text[i++] ) ];

			for ( int r = 0, R = this.regions.Length; r < R; r++ ) {
				
				this.regions[r] = text[i++];
			
			}

			// parse vertices
			this.vertices = new JMSVertex[ int.Parse( text[i++] ) ];

			for ( int v = 0, V = this.vertices.Length; v < V; v++ ) {

				JMSVertex vertex = new JMSVertex();
				
				vertex.node0 = int.Parse( text[i++] );
				
				vertex.position = new Vector3(
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture ),
					float.Parse( text[i++], CultureInfo.InvariantCulture )
				);

				// as Moses' reclaimer has commented out, normals are clamped by tool.exe
				vertex.normal = new Vector3(
					Math.Min( Mathf.Max( float.Parse( text[i++], CultureInfo.InvariantCulture ), -1.0f ), 1.0f ),
					Math.Min( Mathf.Max( float.Parse( text[i++], CultureInfo.InvariantCulture ), -1.0f ), 1.0f ),
					Math.Min( Mathf.Max( float.Parse( text[i++], CultureInfo.InvariantCulture ), -1.0f ), 1.0f )
				);

				vertex.node1 = int.Parse( text[i++] );
				vertex.node1weight = float.Parse( text[i++] );

				vertex.uv = new Vector2( 
					float.Parse( text[i++], CultureInfo.InvariantCulture ), 
					float.Parse( text[i++], CultureInfo.InvariantCulture ) 
				);
				i++; // skip useless w component

				vertex.tangent = new Vector3( 0f, 1f, 0f );
				vertex.binormal = new Vector3( 1f, 0f, 0f );
				
				this.vertices[v] = vertex;

			}
			
			// parse triangles
			this.triangles = new JMSTriangle[ int.Parse( text[i++]) ];

			for ( int t = 0, T = this.triangles.Length; t < T; t++ ) {

				JMSTriangle triangle = new JMSTriangle();

				triangle.region = int.Parse( text[i++] );
				triangle.shader = int.Parse( text[i++] );
				
				triangle.vertices = new int[] {
					int.Parse( text[i++] ),
					int.Parse( text[i++] ),
					int.Parse( text[i++] )
				};

				this.triangles[t] = triangle;

			}

			return;

		}

		// todo: remove
		private Mesh GenerateMesh() {
			Mesh mesh = new Mesh();

			mesh.vertices = this.vertices
				.Select( v => v.position * Constants.RESCALE_MULTIPLY )
				.Select( v => new Vector3(v.x,v.z,v.y) )
				.ToArray();

			mesh.normals = this.vertices
				.Select( n => new Vector3(n.normal.x,n.normal.z,n.normal.y) ).ToArray();

			mesh.uv = this.vertices 
				.Select( uv => new Vector2( uv.uv.x, uv.uv.y )).ToArray();

			mesh.boneWeights = this.vertices
				.Select(
					bw => {
						BoneWeight weight = new BoneWeight();
						weight.boneIndex0 = bw.node0;
						weight.weight0 = 1.0f;
						if ( bw.node1 > -1 ) {
							weight.boneIndex1 = bw.node1;
							weight.weight1 = bw.node1weight;
						};
						return weight;
					}
				).ToArray();
			

			mesh.subMeshCount = materials.Length;
			var submeshes = triangles.GroupBy( t => t.shader );
			foreach( var submesh in submeshes ) 
				mesh.SetTriangles( submesh.SelectMany( e => e.vertices.Reverse() ).ToArray(),submesh.Key, true );
			mesh.RecalculateBounds();
			mesh.RecalculateTangents();

			mesh.name = this.regions[0];

			return mesh;

		}
		private Mesh[] GenerateMeshesh() {
			

			var vertices = this.vertices
				.Select( v => v.position * Constants.RESCALE_MULTIPLY )
				.Select( v => new Vector3(v.x,v.z,v.y) )
				.ToArray();

			var normals = this.vertices
				.Select( n => new Vector3(n.normal.x,n.normal.z,n.normal.y) ).ToArray();

			var uvs = this.vertices // I have to decide if 1 - y has to be done before.
				.Select( uv => new Vector2( uv.uv.x, uv.uv.y )).ToArray();

			var bonesWeights = this.vertices
				.Select(
					bw => {
						BoneWeight weight = new BoneWeight();
						weight.boneIndex0 = bw.node0;
						weight.weight0 = 1.0f;
						if ( bw.node1 > -1 ) {
							weight.boneIndex1 = bw.node1;
							weight.weight1 = bw.node1weight;
						};
						return weight;
					}
				).ToArray();
			

			var regions = this.regions.AsEnumerable().Select( ( name, index ) => {
				var region = new Mesh();
				region.name = name;

				region.vertices = vertices;
				region.normals = normals;
				region.uv = uvs;
				region.boneWeights = bonesWeights;

				var shaders = this.triangles
					.Where( e => e.region == index )
					.GroupBy(e => e.shader );
				
				region.subMeshCount = this.materials.Length;

				foreach ( var shader in shaders )
					region.SetTriangles( shader.SelectMany(e => e.vertices.Reverse()).ToArray(),shader.Key, true );

				
				region.RecalculateTangents();
				region.RecalculateBounds();				

				return region;
			});

			return regions.ToArray();


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
		private GameObject[] GenerateNodes () {
			this.DoParenting();
			GameObject[] nodes = this.nodes.Select(
				node => new GameObject(Constants.NODE_PREFIX + node.name)
			).ToArray();

			for ( int n = 0, N = nodes.Length; n < N; n++ ) {
				if( this.nodes[n].parent_index != -1 )
					nodes[n].transform.parent = nodes[this.nodes[n].parent_index].transform;
			}

			for ( int n = 0, N = nodes.Length; n < N; n++ ) {
				var obj = nodes[n];
				var node = this.nodes[n];

				obj.transform.localPosition = 
					new Vector3(node.position.x,node.position.z,node.position.y) * Constants.RESCALE_MULTIPLY;
				
				var quat = Quaternion.Inverse(node.rotation);
				obj.transform.localRotation =
					new Quaternion( quat.x, quat.z, quat.y, -quat.w);
				
			}
			
			
			return nodes;
		}

		private GameObject[] GenerateMarkers( GameObject[] nodes) {
			
			GameObject[] markers = this.markers.Select(
				(M) => {
					GameObject marker = new GameObject(Constants.MARKER_PREFIX + M.name);
					marker.transform.parent = nodes[M.parent].transform;
					marker.transform.localPosition =
						new Vector3( M.position.x, M.position.z, M.position.y ) * Constants.RESCALE_MULTIPLY;
					marker.transform.localRotation =
						new Quaternion( M.rotation.x, M.rotation.z, M.rotation.y, M.rotation.w );
					return marker;
				}
			).ToArray();
			return markers;
		}
  }
}
