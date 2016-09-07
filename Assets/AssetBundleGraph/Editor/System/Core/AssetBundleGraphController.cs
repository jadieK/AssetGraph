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
	 * AssetBundleGraphController executes operations based on graph 
	 */
	public class AssetBundleGraphController {
//		/**
//		 * Execute Setup operations using current graph
//		 */
//		public static Dictionary<string, Dictionary<string, List<DepreacatedThroughputAsset>>> 
//		PerformSetup (SaveData saveData) {
//			var graphDescription = GraphDescriptionBuilder.BuildGraphDescriptionFromJson(deserializedJsonData);
//			
//			var terminalNodeIds = graphDescription.terminalNodeIds;
//			var allNodes = graphDescription.allNodes;
//			var allConnections = graphDescription.allConnections;
//
//			/*
//				Validation: node names should not overlapping.
//			*/
//			{
//				var nodeNames = allNodes.Select(node => node.nodeName).ToList();
//				var overlappings = nodeNames.GroupBy(x => x)
//					.Where(group => 1 < group.Count())
//					.Select(group => group.Key)
//					.ToList();
//
//				if (overlappings.Any()) {
//					throw new AssetBundleGraphException("Duplicate node name found:" + overlappings[0] + " please rename and avoid same name.");
//				}
//			}
//
//			var resultDict = new Dictionary<string, Dictionary<string, List<Asset>>>();
//			var cacheDict  = new Dictionary<string, List<string>>();
//
//			foreach (var terminalNodeId in terminalNodeIds) {
//				PerformSetupForNode(terminalNodeId, allNodes, allConnections, resultDict, cacheDict);
//			}
//			
//			return CollectResult(resultDict);
//		}

		/**
		 * Execute Run operations using current graph
		 */
		public static Dictionary<string, Dictionary<string, List<DepreacatedThroughputAsset>>> 
		Perform (
			SaveData saveData, 
			BuildTarget target,
			bool isRun,
			Action<string, float> updateHandler=null
		) {			
			/*
				Validation: node names should not overlapping.
				perform only when setup
			*/
			if(!isRun){
				var nodeNames = saveData.Nodes.Select(node => node.Name).ToList();
				var overlappings = nodeNames.GroupBy(x => x)
					.Where(group => 1 < group.Count())
					.Select(group => group.Key)
					.ToList();

				if (overlappings.Any()) {
					throw new AssetBundleGraphException("Duplicate node name found:" + overlappings[0] + " please rename and avoid same name.");
				}
			} else {
				IntegratedGUIBundleBuilder.RemoveAllAssetBundleSettings();
			}

			var resultDict = new Dictionary<string, Dictionary<string, List<Asset>>>();
			var cacheDict  = new Dictionary<string, List<string>>();

			foreach (var leafNode in saveData.CollectAllLeafNodes()) {
				DoNodeOperation(target, leafNode, saveData, resultDict, cacheDict, new List<string>(), isRun, updateHandler);
			}

			return CollectResult(resultDict);
		}

		/**
		 *  Collect build result: connectionId : < groupName : List<Asset> >
		 */
		private static Dictionary<string, Dictionary<string, List<DepreacatedThroughputAsset>>> 
		CollectResult (Dictionary<string, Dictionary<string, List<Asset>>> sourceConId_Group_Throughput) {

			var result = new Dictionary<string, Dictionary<string, List<DepreacatedThroughputAsset>>>();

			foreach (var connectionId in sourceConId_Group_Throughput.Keys) {
				var connectionGroupDict = sourceConId_Group_Throughput[connectionId];

				var newConnectionGroupDict = new Dictionary<string, List<DepreacatedThroughputAsset>>();
				foreach (var groupKey in connectionGroupDict.Keys) {
					var connectionThroughputList = connectionGroupDict[groupKey];

					var sourcePathList = new List<DepreacatedThroughputAsset>();
					foreach (var assetData in connectionThroughputList) {
						var bundled = assetData.isBundled;

						if (!string.IsNullOrEmpty(assetData.importFrom)) {
							sourcePathList.Add(new DepreacatedThroughputAsset(assetData.importFrom, bundled));
							continue;
						} 

						if (!string.IsNullOrEmpty(assetData.absoluteAssetPath)) {
							var relativeAbsolutePath = assetData.absoluteAssetPath.Replace(FileUtility.ProjectPathWithSlash(), string.Empty);
							sourcePathList.Add(new DepreacatedThroughputAsset(relativeAbsolutePath, bundled));
							continue;
						}

						if (!string.IsNullOrEmpty(assetData.exportTo)) {
							sourcePathList.Add(new DepreacatedThroughputAsset(assetData.exportTo, bundled));
							continue;
						}
					}
					newConnectionGroupDict[groupKey] = sourcePathList;
				}
				result[connectionId] = newConnectionGroupDict;
			}
			return result;
		}

//		/**
//			Perform Setup on all serialized nodes respect to graph structure.
//			@result returns ordered connectionIds
//		*/
//		private static List<string> PerformSetupForNode (
//			string endNodeId, 
//			List<NodeData> allNodes, 
//			List<ConnectionData> connections, 
//			Dictionary<string, Dictionary<string, List<Asset>>> resultDict,
//			Dictionary<string, List<string>> cacheDict
//		) {
//			DoNodeOperation(endNodeId, allNodes, connections, resultDict, cacheDict, new List<string>(), false);
//			return resultDict.Keys.ToList();
//		}
//
//		/**
//			Perform Run on all serialized nodes respect to graph structure.
//			@result returns ordered connectionIds
//		*/
//		private static List<string> PerformForNode (
//			string endNodeId, 
//			List<NodeData> allNodes, 
//			List<ConnectionData> connections, 
//			Dictionary<string, Dictionary<string, List<Asset>>> resultDict,
//			Dictionary<string, List<string>> cacheDict,
//			bool isRun,
//			Action<string, float> updateHandler=null
//		) {
//			DoNodeOperation(endNodeId, allNodes, connections, resultDict, cacheDict, new List<string>(), isRun, updateHandler);
//			return resultDict.Keys.ToList();
//		}

		/**
			Perform Run or Setup from parent of given terminal node recursively.
		*/
		private static void DoNodeOperation (
			BuildTarget target,
			NodeData currentNodeData,
			SaveData saveData,
			Dictionary<string, Dictionary<string, List<Asset>>> resultDict, 
			Dictionary<string, List<string>> cachedDict,
			List<string> usedConnectionIds,
			bool isActualRun,
			Action<string, float> updateHandler=null
		) {
//			var relatedNodes = allNodes.Where(relation => relation.nodeId == node.Id).ToList();
//			if (!relatedNodes.Any()) {
//				return;
//			}
//
//			var currentNodeData = relatedNodes[0];

			if (currentNodeData.IsNodeOperationPerformed) {
				return;
			}

			/*
			 * Perform prarent node recursively from this node
			*/
			foreach (var c in currentNodeData.ConnectionsToParent) {

				var parentNodeId = c.FromNodeId;
				if (usedConnectionIds.Contains(c.Id)) {
					throw new NodeException("connection loop detected.", parentNodeId);
				}

				usedConnectionIds.Add(c.Id);

				var parentNode = saveData.Nodes.Where(node => node.Id == parentNodeId).ToList();
				if (!parentNode.Any()) {
					return;
				}

				// check if nodes can connect together
				ConnectionData.ValidateConnection(parentNode[0], currentNodeData);

				DoNodeOperation(target, parentNode[0], saveData, resultDict, cachedDict, usedConnectionIds, isActualRun, updateHandler);
			}

			/*
			 * Perform node operation for this node
			*/

			// connections Ids from this node to child nodes. non-ordered.
			// actual running order depends on order of Node's OutputPoint order.
			var nonOrderedConnectionsFromThisNodeToChildNode = saveData.Connections
				.Where(con => con.FromNodeId == currentNodeData.Id)
				.ToList();

			var orderedNodeOutputPointIds = saveData.Nodes.Where(node => node.Id == currentNodeData.Id).SelectMany(node => node.OutputPoints).Select(point => point.Id).ToList();

			/*
				get connection ids which is orderd by node's outputPoint-order. 
			*/
			var orderedConnectionIds = new List<string>(nonOrderedConnectionsFromThisNodeToChildNode.Count);
			foreach (var orderedNodeOutputPointId in orderedNodeOutputPointIds) {
				foreach (var nonOrderedConnectionFromThisNodeToChildNode in nonOrderedConnectionsFromThisNodeToChildNode) {
					var nonOrderedConnectionOutputPointId = nonOrderedConnectionFromThisNodeToChildNode.FromNodeConnectionPointId;
					if (orderedNodeOutputPointId == nonOrderedConnectionOutputPointId) {
						orderedConnectionIds.Add(nonOrderedConnectionFromThisNodeToChildNode.Id);
						continue;
					} 
				} 
			}

			/*
				FilterNode and BundlizerNode uses specific multiple output connections.
				ExportNode does not have output.
				but all other nodes has only one output connection and uses first connection.
			*/
			var firstConnectionIdFromThisNodeToChildNode = string.Empty;
			if (orderedConnectionIds.Any()) firstConnectionIdFromThisNodeToChildNode = orderedConnectionIds[0];

			if (updateHandler != null) updateHandler(currentNodeData.Id, 0f);

			/*
				has next node, run first time.
			*/

			var alreadyCachedPaths = new List<string>();
			if (cachedDict.ContainsKey(currentNodeData.Id)) alreadyCachedPaths.AddRange(cachedDict[currentNodeData.Id]);

			/*
				load already exist cache from node.
			*/
			alreadyCachedPaths.AddRange(GetCachedDataByNodeKind(target, currentNodeData.Kind, currentNodeData.Id));

			var inputParentResults = new Dictionary<string, List<Asset>>();

			var receivingConnectionIds = saveData.Connections
				.Where(con => con.ToNodeId == currentNodeData.Id)
				.Select(con => con.Id)
				.ToList();

			foreach (var connecionId in receivingConnectionIds) {
				if (!resultDict.ContainsKey(connecionId)) {
					continue;
				}

				var result = resultDict[connecionId];
				foreach (var groupKey in result.Keys) {
					if (!inputParentResults.ContainsKey(groupKey)) inputParentResults[groupKey] = new List<Asset>();
					inputParentResults[groupKey].AddRange(result[groupKey]);	
				}
			}

			/*
				the Action passes to NodeOperaitons.
				It stores result to resultDict.
			*/
			Action<string, string, Dictionary<string, List<Asset>>, List<string>> Output = 
				(string dataSourceNodeId, string targetConnectionId, Dictionary<string, List<Asset>> result, List<string> justCached) => 
			{
				var targetConnectionIds = saveData.Connections
					.Where(con => con.Id == targetConnectionId)
					.Select(con => con.Id)
					.ToList();

				if (!targetConnectionIds.Any()) {
					// if next connection does not exist, no results for next.
					// save results to resultDict with this endpoint node's id.
					resultDict[dataSourceNodeId] = new Dictionary<string, List<Asset>>();
					foreach (var groupKey in result.Keys) {
						if (!resultDict[dataSourceNodeId].ContainsKey(groupKey)) {
							resultDict[dataSourceNodeId][groupKey] = new List<Asset>();
						}
						resultDict[dataSourceNodeId][groupKey].AddRange(result[groupKey]);
					}
					return;
				}

				if (!resultDict.ContainsKey(targetConnectionId)) {
					resultDict[targetConnectionId] = new Dictionary<string, List<Asset>>();
				}

				/*
					merge connection result by group key.
				*/
				foreach (var groupKey in result.Keys) {
					if (!resultDict[targetConnectionId].ContainsKey(groupKey)) {
						resultDict[targetConnectionId][groupKey] = new List<Asset>();
					}
					resultDict[targetConnectionId][groupKey].AddRange(result[groupKey]);
				}

				if (isActualRun) {
					if (!cachedDict.ContainsKey(currentNodeData.Id)) {
						cachedDict[currentNodeData.Id] = new List<string>();
					}
					cachedDict[currentNodeData.Id].AddRange(justCached);
				}
			};

			try {
				INodeOperationBase executor = CreateOperation(saveData, currentNodeData);
				if(executor != null) {
					if(isActualRun) {
						executor.Run(target, currentNodeData, firstConnectionIdFromThisNodeToChildNode, inputParentResults, alreadyCachedPaths, Output);
					}
					else {
						executor.Setup(target, currentNodeData, firstConnectionIdFromThisNodeToChildNode, inputParentResults, alreadyCachedPaths, Output);
					}
				}

			} catch (NodeException e) {
				AssetBundleGraphEditorWindow.AddNodeException(e);
				//Debug.LogError("error occured:\"" + e.reason + "\", please check information on node.");
				return;
				//throw new AssetBundleGraphException(node.Name + ": " + e.reason);
			}

			currentNodeData.IsNodeOperationPerformed = true;
			if (updateHandler != null) {
				updateHandler(currentNodeData.Id, 1f);
			}
		}

		public static INodeOperationBase CreateOperation(SaveData saveData, NodeData currentNodeData) {
			INodeOperationBase executor = null;

			try {
				switch (currentNodeData.Kind) {
				case AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT: {
						var scriptClassName = currentNodeData.ScriptClassName;
						executor = SystemDataUtility.CreateNodeOperationInstance<FilterBase>(scriptClassName, currentNodeData);
						break;
					}
				case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT: {
						var scriptClassName = currentNodeData.ScriptClassName;
						executor = SystemDataUtility.CreateNodeOperationInstance<PrefabricatorBase>(scriptClassName, currentNodeData);
						break;
					}
				case AssetBundleGraphSettings.NodeKind.LOADER_GUI: {
						executor = new IntegratedGUILoader();
						break;
					}
				case AssetBundleGraphSettings.NodeKind.FILTER_GUI: {
						/**
								Filter requires "outputPoint ordered exist connection Id and Fake connection Id" for
								exhausting assets by keyword and type correctly.

								outputPoint which has connection can through assets by keyword and keytype,
								also outputPoint which doesn't have connection should take assets by keyword and keytype.
						*/
						var orderedNodeOutputPointIds = saveData.Nodes.Where(node => node.Id == currentNodeData.Id).SelectMany(node => node.OutputPoints).Select(p => p.Id).ToList();
						var nonOrderedConnectionsFromThisNodeToChildNode = saveData.Connections.Where(con => con.FromNodeId == currentNodeData.Id).ToList();
						var orderedConnectionIdsAndFakeConnectionIds = new string[orderedNodeOutputPointIds.Count];

						for (var i = 0; i < orderedNodeOutputPointIds.Count; i++) {
							var orderedNodeOutputPointId = orderedNodeOutputPointIds[i];

							foreach (var nonOrderedConnectionFromThisNodeToChildNode in nonOrderedConnectionsFromThisNodeToChildNode) {
								var connectionOutputPointId = nonOrderedConnectionFromThisNodeToChildNode.FromNodeConnectionPointId;
								if (orderedNodeOutputPointId == connectionOutputPointId) {
									orderedConnectionIdsAndFakeConnectionIds[i] = nonOrderedConnectionFromThisNodeToChildNode.Id;
									break;
								} else {
									orderedConnectionIdsAndFakeConnectionIds[i] = AssetBundleGraphSettings.FILTER_FAKE_CONNECTION_ID;
								}
							}
						}
						executor = new IntegratedGUIFilter(orderedConnectionIdsAndFakeConnectionIds);
						break;
					}

				case AssetBundleGraphSettings.NodeKind.IMPORTSETTING_GUI: {
						executor = new IntegratedGUIImportSetting();
						break;
					}
				case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI: {
						executor = new IntegratedGUIModifier(currentNodeData.ScriptClassName);
						break;
					}
				case AssetBundleGraphSettings.NodeKind.GROUPING_GUI: {
						executor = new IntegratedGUIGrouping();
						break;
					}
				case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI: {
						var scriptClassName = currentNodeData.ScriptClassName;
						if (string.IsNullOrEmpty(scriptClassName)) {
							throw new NodeException(currentNodeData.Name + ": Classname is empty. Set valid classname. Configure valid script name from editor.", currentNodeData.Id);
						}
						executor = SystemDataUtility.CreateNodeOperationInstance<PrefabricatorBase>(scriptClassName, currentNodeData);
						break;
					}

				case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI: {
						/*
								Bundlizer requires assetOutputConnectionId and additional resourceOutputConnectionId.
								both-connected, or both-not-connected, or one of them is connected. 4 patterns exists.
								
								Bundler Node's outputPoint [0] is always the point for assetOutputConnectionId.
								Bundler Node's outputPoint [1] is always the point for resourceOutputConnectionId.
								
								if one of these outputPoint don't have connection, use Fake connection id for correct output.

								
								unorderedConnectionId \
														----> orderedConnectionIdsAndFakeConnectionIds. 
								orderedOutputPointId  / 
							*/
						var orderedNodeOutputPointIds = saveData.Nodes.Where(node => node.Id == currentNodeData.Id).SelectMany(node => node.OutputPoints).Select(p => p.Id).ToList();
						var nonOrderedConnectionsFromThisNodeToChildNode = saveData.Connections.Where(con => con.FromNodeId == currentNodeData.Id).ToList();
						var orderedConnectionIdsAndFakeConnectionIds = new string[orderedNodeOutputPointIds.Count];
						for (var i = 0; i < orderedNodeOutputPointIds.Count; i++) {
							var orderedNodeOutputPointId = orderedNodeOutputPointIds[i];

							foreach (var nonOrderedConnectionFromThisNodeToChildNode in nonOrderedConnectionsFromThisNodeToChildNode) {
								var connectionOutputPointId = nonOrderedConnectionFromThisNodeToChildNode.FromNodeConnectionPointId;
								if (orderedNodeOutputPointId == connectionOutputPointId) {
									orderedConnectionIdsAndFakeConnectionIds[i] = nonOrderedConnectionFromThisNodeToChildNode.Id;
									break;
								} else {
									orderedConnectionIdsAndFakeConnectionIds[i] = AssetBundleGraphSettings.BUNDLIZER_FAKE_CONNECTION_ID;
								}
							}
						}

						executor = new IntegratedGUIBundlizer(orderedConnectionIdsAndFakeConnectionIds[0]);
						break;
					}

				case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI: {
						executor = new IntegratedGUIBundleBuilder();
						break;
					}

				case AssetBundleGraphSettings.NodeKind.EXPORTER_GUI: {
						executor = new IntegratedGUIExporter();
						break;
					}

				default: {
						Debug.LogError(currentNodeData.Name + " is defined as unknown kind of node. value:" + currentNodeData.Kind);
						break;
					}
				} 
			} catch (NodeException e) {
				AssetBundleGraphEditorWindow.AddNodeException(e);
				//Debug.LogError("error occured:\"" + e.reason + "\", please check information on node.");
				//throw new AssetBundleGraphException(node.Name + ": " + e.reason);
			}

			return executor;
		}

		public static List<string> GetCachedDataByNodeKind (BuildTarget t, AssetBundleGraphSettings.NodeKind nodeKind, string nodeId) {
			switch (nodeKind) {
				case AssetBundleGraphSettings.NodeKind.IMPORTSETTING_GUI: {
					// no cache file exists for importSetting.
					return new List<string>();
				}
				case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI: {
					// no cache file exists for modifier.
					return new List<string>();
				}
				
				case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT:
				case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI: {
					var cachedPathBase = FileUtility.PathCombine(
						AssetBundleGraphSettings.PREFABRICATOR_CACHE_PLACE, 
						nodeId,
						SystemDataUtility.GetPathSafeTargetName(t)
					);

					// no cache folder, no cache.
					if (!Directory.Exists(cachedPathBase)) {
						// search default platform + package
						cachedPathBase = FileUtility.PathCombine(
							AssetBundleGraphSettings.PREFABRICATOR_CACHE_PLACE, 
							nodeId,
							SystemDataUtility.GetPathSafeDefaultTargetName()
						);

						if (!Directory.Exists(cachedPathBase)) {
							return new List<string>();
						}
					}

					return FileUtility.FilePathsInFolder(cachedPathBase);
				}
				 
				case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI: {
					// do nothing.
					break;
				}

				case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI: {
					var cachedPathBase = FileUtility.PathCombine(
						AssetBundleGraphSettings.BUNDLEBUILDER_CACHE_PLACE, 
						nodeId,
						SystemDataUtility.GetPathSafeTargetName(t)
					);

					// no cache folder, no cache.
					if (!Directory.Exists(cachedPathBase)) {
						// search default platform + package
						cachedPathBase = FileUtility.PathCombine(
							AssetBundleGraphSettings.BUNDLEBUILDER_CACHE_PLACE, 
							nodeId,
							SystemDataUtility.GetPathSafeDefaultTargetName()
						);

						if (!Directory.Exists(cachedPathBase)) {
							return new List<string>();
						}
					}

					return FileUtility.FilePathsInFolder(cachedPathBase);
				}

				default: {
					// nothing to do.
					break;
				}
			}
			return new List<string>();
		}
		

	}
}