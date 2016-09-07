using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace AssetBundleGraph {

	/*
	 * node data saved in/to Json
	 */ 
	public partial class NodeData {

		private const string NODE_NAME = "name";
		private const string NODE_ID = "id";
		private const string NODE_KIND = "kind";
		private const string NODE_SCRIPT_CLASSNAME = "scriptClassName";
		private const string NODE_POS = "pos";
		private const string NODE_POS_X = "x";
		private const string NODE_POS_Y = "y";
		private const string NODE_OUTPUTPOINT_LABELS = "outputPointLabels";
		private const string NODE_OUTPUTPOINT_IDS = "outputPointIds";

		//loader settings
		private const string NODE_LOADER_LOAD_PATH = "loadPath";

		//exporter settings
		private const string NODE_EXPORTER_EXPORT_PATH = "exportTo";

		//filter settings
		private const string NODE_FILTER_CONTAINS_KEYWORDS = "filterContainsKeywords";
		private const string NODE_FILTER_CONTAINS_KEYTYPES = "filterContainsKeytypes";

		//group settings
		private const string NODE_GROUPING_KEYWORD = "groupingKeyword";

		//bundlizer settings
		private const string NODE_BUNDLIZER_BUNDLENAME_TEMPLATE = "bundleNameTemplate";
		private const string NODE_BUNDLIZER_VARIANTS = "variants";

		//bundlebuilder settings
		private const string NODE_BUNDLEBUILDER_ENABLEDBUNDLEOPTIONS = "enabledBundleOptions";

		public struct FilterEntry {
			private readonly string m_filterKeyword;
			private readonly string m_filterKeytype;

			public FilterEntry(string keyword, string keytype) {
				m_filterKeyword = keyword;
				m_filterKeytype = keytype;
			}

			public string FilterKeyword {
				get {
					return m_filterKeyword;
				}
			}
			public string FilterKeytype {
				get {
					return m_filterKeytype; 
				}
			}
			public int Hash {
				get {
					var md5 = MD5.Create();
					md5.Initialize();
					md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(m_filterKeyword));
					md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(m_filterKeytype));
					return md5.GetHashCode();
				}
			}
		}

		private string m_name;
		private string m_id;
		private AssetBundleGraphSettings.NodeKind m_kind;
		private int m_x;
		private int m_y;
		private string m_scriptClassName;
		private List<FilterEntry> m_filter;
		private List<ConnectionPointData> 	m_outputPoints; 
		private MultiTargetProperty<string> m_loaderLoadPath;
		private MultiTargetProperty<string> m_exporterExportPath;
		private MultiTargetProperty<string> m_groupingKeyword;
		private MultiTargetProperty<string> m_bundlizerBundleNameTemplate;
		private Dictionary<string, string> 	m_variants;
		private MultiTargetProperty<int> 	m_bundleBuilderEnabledBundleOptions;

		private List<ConnectionData> m_connections;
		private bool m_isNodeOperationPerformed;

		public NodeData() {
			m_name = string.Empty;
			m_id = Guid.NewGuid().ToString();
		}

		public NodeData(Dictionary<string, object> jsonData) {

			m_name = jsonData[NODE_NAME] as string;
			m_id = jsonData[NODE_ID]as string;
			m_kind = AssetBundleGraphSettings.NodeKindFromString(jsonData[NODE_KIND] as string);
			m_connections = new List<ConnectionData>();

			var pos = jsonData[NODE_POS] as Dictionary<string, object>;
			m_x = Convert.ToInt32(pos[NODE_POS_X]);
			m_y = Convert.ToInt32(pos[NODE_POS_Y]);
			var ids    = jsonData[NODE_OUTPUTPOINT_IDS] as List<object>;
			var labels = jsonData[NODE_OUTPUTPOINT_LABELS] as List<object>;
			m_outputPoints = new List<ConnectionPointData>();
			for(int i=0; i< ids.Count; ++i) {
				m_outputPoints.Add(new ConnectionPointData(ids[i] as string, labels[i] as string));
			}

			switch (m_kind) {
			case AssetBundleGraphSettings.NodeKind.IMPORTSETTING_GUI:
				// nothing to do
				break;
			case AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT:
			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT:
			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI:
			case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI:
				{
					if(jsonData.ContainsKey(NODE_SCRIPT_CLASSNAME)) {
						m_scriptClassName = jsonData[NODE_SCRIPT_CLASSNAME] as string;
					}
				}
				break;
			case AssetBundleGraphSettings.NodeKind.LOADER_GUI:
				{
					m_loaderLoadPath = new MultiTargetProperty<string>(jsonData[NODE_LOADER_LOAD_PATH] as Dictionary<string, object>);
				}
				break;
			case AssetBundleGraphSettings.NodeKind.FILTER_GUI:
				{
					var keywords = jsonData[NODE_FILTER_CONTAINS_KEYWORDS] as List<object>;
					var keytypes = jsonData[NODE_FILTER_CONTAINS_KEYTYPES] as List<object>;

					m_filter = new List<FilterEntry>();

					for(int i=0; i<keywords.Count; ++i) {
						m_filter.Add(new FilterEntry(keywords[i] as string, keytypes[i] as string));
					}
				}
				break;
			case AssetBundleGraphSettings.NodeKind.GROUPING_GUI:
				{
					m_groupingKeyword = new MultiTargetProperty<string>(jsonData[NODE_GROUPING_KEYWORD] as Dictionary<string, object>);
				}
				break;
			case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI:
				{
					m_bundlizerBundleNameTemplate = new MultiTargetProperty<string>(jsonData[NODE_BUNDLIZER_BUNDLENAME_TEMPLATE] as Dictionary<string, object>);
					m_variants = new Dictionary<string, string>();
					if(jsonData.ContainsKey(NODE_BUNDLIZER_VARIANTS)){
						var src = jsonData[NODE_BUNDLIZER_VARIANTS] as Dictionary<string, object>;
						foreach(var v in src) {
							m_variants.Add(v.Key, v.Value as string);
						}
					}
				}
				break;
			case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI:
				{
					m_bundleBuilderEnabledBundleOptions = new MultiTargetProperty<int>(jsonData[NODE_BUNDLEBUILDER_ENABLEDBUNDLEOPTIONS] as Dictionary<string, object>);
				}
				break;
			case AssetBundleGraphSettings.NodeKind.EXPORTER_GUI:
				{
					m_exporterExportPath = new MultiTargetProperty<string>(jsonData[NODE_EXPORTER_EXPORT_PATH] as Dictionary<string, object>);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}

		}

		public NodeData(NodeGUI nodeGui) {

			m_id = nodeGui.nodeId;
			m_name = nodeGui.name;
			m_x = nodeGui.GetX();
			m_y = nodeGui.GetY();
			m_kind = nodeGui.kind;

			m_outputPoints = new List<ConnectionPointData>();
			List<string> ids = nodeGui.OutputPointIds();
			List<string> labels = nodeGui.OutputPointLabels();

			for(int i=0; i<ids.Count;++i) {
				m_outputPoints.Add(new ConnectionPointData(ids[i], labels[i]));
			}

			switch(m_kind) {
			case AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT:
			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT:
			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI:
			case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI:
				m_scriptClassName 	= nodeGui.scriptClassName;
				break;

			case AssetBundleGraphSettings.NodeKind.FILTER_GUI:
				m_filter = new List<FilterEntry>();
				for(int i=0; i<nodeGui.filterContainsKeytypes.Count; ++i) {
					m_filter.Add(new FilterEntry(nodeGui.filterContainsKeywords[i], nodeGui.filterContainsKeytypes[i]));
				}
				break;

			case AssetBundleGraphSettings.NodeKind.LOADER_GUI:
				m_loaderLoadPath = nodeGui.loadPath.ToProperty();
				break;
			
			case AssetBundleGraphSettings.NodeKind.GROUPING_GUI:
				m_groupingKeyword = nodeGui.groupingKeyword.ToProperty();
				break;

			case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI:
				m_bundlizerBundleNameTemplate = nodeGui.bundleNameTemplate.ToProperty();
				m_variants = nodeGui.variants.ReadonlyDict();
				break;

			case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI:
				m_bundleBuilderEnabledBundleOptions = nodeGui.enabledBundleOptions.ToProperty();
				break;

			case AssetBundleGraphSettings.NodeKind.EXPORTER_GUI:
				m_exporterExportPath = nodeGui.exportTo.ToProperty();
				break;
			}
		}

		public void AddConnectionToParent (ConnectionData connection) {
			m_connections.Add(connection);
		}

		public string Name {
			get {
				return m_name;
			}
		}
		public string Id {
			get {
				return m_id;
			}
		}
		public AssetBundleGraphSettings.NodeKind Kind {
			get {
				return m_kind;
			}
		}
		public string ScriptClassName {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT, 
					AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT,
					AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI,
					AssetBundleGraphSettings.NodeKind.MODIFIER_GUI
				);
				return m_scriptClassName;
			}
		}

		public int X {
			get {
				return m_x;
			}
		}
		public int Y {
			get {
				return m_y;
			}
		}
		public List<ConnectionPointData> OutputPoints {
			get {
				return m_outputPoints;
			}
		}

		public MultiTargetProperty<string> LoaderLoadPath {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.LOADER_GUI 
				);
				return m_loaderLoadPath;
			}
		}

		public MultiTargetProperty<string> ExporterExportPath {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.EXPORTER_GUI 
				);
				return m_exporterExportPath;
			}
		}

		public MultiTargetProperty<string> GroupingKeywords {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.GROUPING_GUI 
				);
				return m_groupingKeyword;
			}
		}

		public MultiTargetProperty<string> BundleNameTemplate {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI 
				);
				return m_bundlizerBundleNameTemplate;
			}
		}

		public Dictionary<string, string> Variants {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI 
				);
				return m_variants;
			}
		}

		public MultiTargetProperty<int> BundleBuilderBundleOptions {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI 
				);
				return m_bundleBuilderEnabledBundleOptions;
			}
		}

		public List<FilterEntry> FilterConditions {
			get {
				ValidateAccess(
					AssetBundleGraphSettings.NodeKind.FILTER_GUI
				);
				return m_filter;
			}
		}

		public bool IsNodeOperationPerformed {
			get {
				return m_isNodeOperationPerformed;
			}
			set {
				m_isNodeOperationPerformed = true;
			}
		}

		public List<ConnectionData> ConnectionsToParent {
			get {
				return m_connections;
			}
		}

		public string GetLoaderFullLoadPath(BuildTarget g) {
			return FileUtility.PathCombine(Application.dataPath, LoaderLoadPath[g]);
		}

		public bool ValidateOverlappingFilterCondition(bool throwException) {
			ValidateAccess(AssetBundleGraphSettings.NodeKind.FILTER_GUI);

			var conditionGroup = FilterConditions.GroupBy(v => v.Hash).ToList();

			bool hasOverlap = conditionGroup.Where(g => g.Count() > 1).Any();

			if( hasOverlap && throwException ) {
				var badCond = conditionGroup.Where(g => g.Count() > 1).First().First();
				throw new NodeException(String.Format("Duplicated filter condition found for [Keyword:{0} Type:{1}]", badCond.FilterKeyword, badCond.FilterKeytype), Id);
			}
			return hasOverlap;
		}

		private void ValidateAccess(params AssetBundleGraphSettings.NodeKind[] allowedKind) {
			foreach(var k in allowedKind) {
				if (k == m_kind) {
					return;
				}
			}
			throw new AssetBundleGraphException(m_name + ": Tried to access invalid method or property.");
		}

		public Dictionary<string, object> ToJsonDictionary() {
			var nodeDict = new Dictionary<string, object>();

			nodeDict[NODE_NAME] = m_name;
			nodeDict[NODE_ID] 	= m_id;
			nodeDict[NODE_KIND] = m_kind.ToString();

			nodeDict[NODE_OUTPUTPOINT_LABELS] = m_outputPoints.Select(p => p.Label).ToList();
			nodeDict[NODE_OUTPUTPOINT_IDS] 	  = m_outputPoints.Select(p => p.Id).ToList();

			nodeDict[NODE_POS] = new Dictionary<string, object>() {
				{NODE_POS_X, m_x},
				{NODE_POS_Y, m_y}
			};

			switch (m_kind) {
			case AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT:
			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT:
			case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI:
				nodeDict[NODE_SCRIPT_CLASSNAME] = m_scriptClassName;
				break;
			case AssetBundleGraphSettings.NodeKind.LOADER_GUI:
				nodeDict[NODE_LOADER_LOAD_PATH] = m_loaderLoadPath.ToJsonDictionary();
				break;
			case AssetBundleGraphSettings.NodeKind.FILTER_GUI:
				nodeDict[NODE_FILTER_CONTAINS_KEYWORDS] = m_filter.Select(f => f.FilterKeyword).ToList();
				nodeDict[NODE_FILTER_CONTAINS_KEYTYPES] = m_filter.Select(f => f.FilterKeytype).ToList();
				break;
			case AssetBundleGraphSettings.NodeKind.GROUPING_GUI:
				nodeDict[NODE_GROUPING_KEYWORD] = m_groupingKeyword.ToJsonDictionary();
				break;
			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI:
				break;
			case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI:
				nodeDict[NODE_BUNDLIZER_BUNDLENAME_TEMPLATE] = m_bundlizerBundleNameTemplate.ToJsonDictionary();
				nodeDict[NODE_BUNDLIZER_VARIANTS] = m_variants;
				break;
			case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI:
				nodeDict[NODE_BUNDLEBUILDER_ENABLEDBUNDLEOPTIONS] = m_bundleBuilderEnabledBundleOptions.ToJsonDictionary();
				break;
			case AssetBundleGraphSettings.NodeKind.EXPORTER_GUI:
				nodeDict[NODE_EXPORTER_EXPORT_PATH] = m_exporterExportPath.ToJsonDictionary();
				break;
			case AssetBundleGraphSettings.NodeKind.IMPORTSETTING_GUI:
				// nothing to do
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}

			return nodeDict;
		}

		public string ToJsonString() {
			return AssetBundleGraph.Json.Serialize(ToJsonDictionary());
		}
	}

//	public class NodeData {
//		public readonly string nodeName;
//		public readonly string nodeId;
//		public readonly AssetBundleGraphSettings.NodeKind nodeKind;
//		public readonly List<string> outputPointIds;
//
//		// for All script nodes & prefabricator, bundlizer GUI.
//		public readonly string scriptClassName;
//
//		// for Loader Script
//		public readonly Dictionary<string, string> loadFilePath;
//
//		// for Exporter Script
//		public readonly Dictionary<string, string> exportFilePath;
//
//		// for Filter GUI data
//		public readonly List<string> containsKeywords;
//		public readonly List<string> containsKeytypes;
//
//		// for Modifier GUI data
//		public readonly Dictionary<string, string> modifierPackages;
//
//		// for Grouping GUI data
//		public readonly Dictionary<string, string> groupingKeyword;
//
//		// for Bundlizer GUI data
//		public readonly Dictionary<string, string> bundleNameTemplate;
//		public readonly Dictionary<string, string> variants;
//
//		// for BundleBuilder GUI data
//		public readonly Dictionary<string, List<string>> enabledBundleOptions;
//
//		
//		public List<ConnectionData> connectionToParents = new List<ConnectionData>();
//
//		private bool done;
//
//		public NodeData (
//			string nodeId, 
//		AssetBundleGraphSettings.NodeKind nodeKind,
//			string nodeName,
//			List<string> outputPointIds,
//			string scriptClassName = null,
//			Dictionary<string, string> loadPath = null,
//			Dictionary<string, string> exportTo = null,
//			List<string> filterContainsKeywords = null,
//			List<string> filterContainsKeytypes = null,
//			Dictionary<string, string> modifierPackages = null,
//			Dictionary<string, string> groupingKeyword = null,
//			Dictionary<string, string> bundleNameTemplate = null,
//			Dictionary<string, string> variants = null,
//			Dictionary<string, List<string>> enabledBundleOptions = null
//		) {
//			this.nodeId = nodeId;
//			this.nodeKind = nodeKind;
//			this.nodeName = nodeName;
//			this.outputPointIds = outputPointIds;
//
//			this.scriptClassName = null;
//			this.loadFilePath = null;
//			this.exportFilePath = null;
//			this.containsKeywords = null;
//			this.modifierPackages = null;
//			this.groupingKeyword = null;
//			this.variants = null;
//			this.bundleNameTemplate = null;
//			this.enabledBundleOptions = null;
//
//			switch (nodeKind) {
//				case AssetBundleGraphSettings.NodeKind.LOADER_GUI: {
//					this.loadFilePath = loadPath;
//					break;
//				}
//				case AssetBundleGraphSettings.NodeKind.EXPORTER_GUI: {
//					this.exportFilePath = exportTo;
//					break;
//				}
//
//				case AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT:
//				case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT:
//				case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI: {
//					this.scriptClassName = scriptClassName;
//					break;
//				}
//
//				case AssetBundleGraphSettings.NodeKind.FILTER_GUI: {
//					this.containsKeywords = filterContainsKeywords;
//					this.containsKeytypes = filterContainsKeytypes;
//					break;
//				}
//
//				case AssetBundleGraphSettings.NodeKind.IMPORTSETTING_GUI: {
//					break;
//				}
//
//				case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI: {
//					this.modifierPackages = modifierPackages;
//					break;
//				}
//				
//				case AssetBundleGraphSettings.NodeKind.GROUPING_GUI: {
//					this.groupingKeyword = groupingKeyword;
//					break;
//				}
//
//				case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI: {
//					this.bundleNameTemplate = bundleNameTemplate;
//					this.variants = variants;
//					break;
//				}
//
//				case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI: {
//					this.enabledBundleOptions = enabledBundleOptions;
//					break;
//				}
//
//				default: {
//					Debug.LogError(node.Name + " is defined as unknown kind of node. value:" + nodeKind);
//					break;
//				}
//			}
//		}
//
//		public void AddConnectionToParent (ConnectionData connection) {
//			connectionToParents.Add(new ConnectionData(connection));
//		}
//
//		public void Done () {
//			done = true;
//		}
//
//		public bool IsAlreadyDone () {
//			return done;
//		}
//	}
}
