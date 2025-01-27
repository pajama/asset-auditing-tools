using System;
using System.Collections.Generic;
using AssetTools.GUIUtility;
using UnityEditor;
using UnityEngine;

namespace AssetTools
{
	[Serializable]
	public class PostprocessorImportTask : BaseImportTask
	{
		/// <summary>
		/// TODO If any of this is changed, do the Assets imported by it need to be reimported?
		/// </summary>

		[SerializeField] private string m_MethodString = "";
		[SerializeField] private string m_Data = "";

		private PostprocessorImportTaskInspector m_Inspector = null;
		internal ProcessorMethodInfo m_ProcessorMethodInfo;
		
		public string methodString
		{
			get { return m_MethodString; }
		}
		
		public override Type GetConformObjectType()
		{
			return typeof(PostprocessorConformObject);
		}
		
		public override ProcessingType TaskProcessType
		{
			get { return ProcessingType.Post; }
		}
		
		public override string ImportTaskName
		{
			get { return "PostprocessorImportTask"; }
		}
		

		public override int Version
		{
			get { return Method == null ? 0 : Method.Version; }
		}

		public override string AssetMenuFixString
		{
			get
			{
				return Method == null ? "None Selected" : "Import using " + Method.TypeName;
			}
		}
		
		public override bool CanProcess( AssetImporter item )
		{
			return true;
		}

		public override List<IConformObject> GetConformObjects( string asset, ImportDefinitionProfile profile )
		{
			// postcessor versionCode comparison
			// will need someway to store this. It could not work well if imported not using it
			// 1: add it to meta data. Only option is userData, which could conflict with other code packages. This would make it included in the hash for cache server. Which would be required.
			// 2: store a databaseof imported version data. Could be tricky to keep in sync
			// 3: AssetDatabaseV2 supports asset dependencies
			
			List<IConformObject> infos = new List<IConformObject>();

			if( Method == null )
			{
				PostprocessorConformObject conformObject = new PostprocessorConformObject( "None Selected", 0 );
				infos.Add( conformObject );
				return infos;
			}

			UserDataSerialization userData = UserDataSerialization.Get( asset );
			List<UserDataSerialization.PostprocessorData> data = userData.GetProcessedMethodsData();
			string profileGuid = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath( profile ) );

			if( data != null )
			{
				for( int i = 0; i < data.Count; ++i )
				{
					if( data[i].moduleName != ImportTaskName ||
					    data[i].typeName != Method.TypeName ||
					    data[i].assemblyName != Method.AssemblyName ||
					    data[i].importDefinitionGUID != profileGuid )
						continue;

					infos.Add( new PostprocessorConformObject( Method.TypeName, Method.Version, data[i].version ) );
					break;
				}
			}
			
			if( infos.Count == 0 )
				infos.Add( new PostprocessorConformObject( Method.TypeName, Method.Version ) );
			
			return infos;
		}

		public override void PreprocessTask( ImportContext context, ImportDefinitionProfile profile )
		{
			UserDataSerialization data = UserDataSerialization.Get( context.AssetPath );
			string profileGuid = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath( profile ) );
			data.UpdateProcessing( new UserDataSerialization.PostprocessorData( profileGuid, ImportTaskName, Method.AssemblyName, Method.TypeName, Method.Version ) );
		}

		public override bool Apply( ImportContext context, ImportDefinitionProfile fromProfile )
		{
			if( string.IsNullOrEmpty( m_MethodString ) == false )
			{
				if( Method != null )
				{
					object returnValue = Method.Invoke( context, m_Data );
					if( returnValue != null )
						return (bool) returnValue;
				}
			}
			return false;
		}
		
		private ProcessorMethodInfo Method
		{
			get
			{
				if( m_ProcessorMethodInfo == null && string.IsNullOrEmpty( m_MethodString ) == false )
				{
					string assemblyName;
					string typeString;
					GetMethodStrings( out assemblyName, out typeString );
					if( string.IsNullOrEmpty( typeString ) )
					{
						Debug.LogError( "Error collecting method from " + m_MethodString );
						return null;
					}
					
					List<ProcessorMethodInfo> methods = PostprocessorImplementorCache.Methods;
					for( int i = 0; i < methods.Count; ++i )
					{
						if( assemblyName != null && methods[i].AssemblyName.StartsWith( assemblyName ) == false )
							continue;

						if( methods[i].TypeName.EndsWith( typeString ) )
						{
							// make sure its correct?
							m_ProcessorMethodInfo = methods[i];
							break;
						}
					}
				}
		
				return m_ProcessorMethodInfo;
			}
		}

		private void GetMethodStrings( out string assemblyName, out string typeString )
		{
			int commaIndex = m_MethodString.IndexOf( ',' );
			if( commaIndex > 0 )
			{
				assemblyName = m_MethodString.Substring( commaIndex + 2 );
				typeString = m_MethodString.Substring( 0, commaIndex );
			}
			else
			{
				assemblyName = "";
				typeString = m_MethodString;
			}
		}
		
		public override void DrawGUI( ControlRect layout )
		{
			if( m_Inspector == null )
				m_Inspector = new PostprocessorImportTaskInspector();
			
			m_Inspector.Draw( SelfSerializedObject, layout );
			SelfSerializedObject.ApplyModifiedProperties();
		}
	}
}