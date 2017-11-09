﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using ClrMDRIndex;
using GraphX.Controls;
using GraphX.PCL.Common.Enums;
using Binding = System.Windows.Data.Binding;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using ListView = System.Windows.Controls.ListView;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;
using TreeView = System.Windows.Controls.TreeView;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using Microsoft.Msagl.WpfGraphControl;
using Microsoft.Msagl.Drawing;
using SubgraphDictionary =
    System.Collections.Generic.SortedDictionary
    <int,
        ClrMDRIndex.triple
        <System.Collections.Generic.List<System.Collections.Generic.List<int>>,
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Collections.Generic.List<int>, int>>,
            int>>;
using System.Collections.Concurrent;
using Microsoft.Diagnostics.Runtime;

namespace MDRDesk
{
    public partial class MainWindow
    {
        #region report names

        private const string ReportTitleStringUsage = "String Usage";
        private const string ReportNameStringUsage = "StringUsage";
        private const string ReportTitleSTypesWithString = "Types With String";
        private const string ReportNameTypesWithString = "TypesWithString";
        private const string ReportTitleSizeInfo = "Type Sizes";
        private const string ReportTitleBaseSizeInfo = "Base Type Sizes";
        private const string ReportNameSizeInfo = "SizesInfo";
        private const string ReportNameBaseSizeInfo = "BaseSizesInfo";
        private const string ReportNameWeakReferenceInfo = "WeakReferenceInfo";
        private const string ReportTitleWeakReferenceInfo = "WeakReference Information";
        private const string ReportTitleInstRef = "Instance Refs";
        private const string ReportNameInstRef = "InstRef";
        private const string ReportTitleSizeDiffs = "Count;/Size Comp";
        private const string ReportNameSizeDiffs = "SizesDiff";

        #endregion report names

        #region grid names

        // type display grids
        //
        private const string GridKeyNamespaceTypeView = "NamespaceTypeView";
        private const string GridKeyNameTypeView = "NameTypeView";

        private const string NameNamespaceTypeNameGrid = "NamespaceTypeView";
        private const string GridNameTypeView = "NameTypeView";
        private const string GridReversedNameTypeView = "ReversedNameTypeView";

        private const string DeadlockGraphGrid = "DeadlockGraphGrid";
        private const string ThreadBlockingGraphGrid = "ThreadBlockingGraphGrid";
        private const string ThreadBlockingObjectGraphGrid = "ThreadBlockingObjectsGraphGrid";  // using MSAGL

        private const string ThreadViewGrid = "ThreadViewGrid";
        private const string AncestorTreeViewGrid = "AncestorTreeViewGrid";

        private const string GridFinalizerQueue = "FinalizerQueueGrid";
        private const string WeakReferenceViewGrid = "WeakReferenceViewGrid";
        private const string FinalizerQueueListView = "FinalizerQueueListView";
        private const string FinalizerQueAddrListBox = "FinalizerQueAddresses";
        private const string FinalizerQueTextBox = "FinalizerQueTextBox";

        private const string TypeValuesReportGrid = "TypeValuesGrid";
        private const string TypeValuesReportView = "TypeValuesView";
        private const string TypeValuesReportInfo = "TypeValuesInformation";

        // writing report supported
        //
        private const string ListingGrid = "ListingGrid";
        private const string ListingGridView = "ListingView";
        private const string ListingGridInformation = "ListingInformation";

        private const string RootsGrid = "RootsGrid";
        private const string RootsGrid_RootTypeList = "RootTypeList";

        private const string GraphGrid = "GraphGrid";


        private string GetReportTitle(ListView lst)
        {
            if (lst.Tag is Tuple<ListingInfo, string>)
            {
                string name = (lst.Tag as Tuple<ListingInfo, string>).Item2;
                return name ?? String.Empty;
            }
            return String.Empty;
        }

        private bool IsReport(ListView lst, string title)
        {
            if (lst.Tag is Tuple<ListingInfo, string>)
            {
                string name = (lst.Tag as Tuple<ListingInfo, string>).Item2;
                if (Utils.SameStrings(title, name)) return true;
            }
            return false;
        }

        #endregion grid names

        #region graphs

        #region deadlock

        public bool DisplayDeadlock(int[][] deadlocks, out string error)
        {
            error = null;
            try
            {
                var grid = this.TryFindResource("DeadlockGraphXGrid") as Grid;
                Debug.Assert(grid != null);
                grid.Name = DeadlockGraphGrid + "__" + Utils.GetNewID();
                var zoomctrl = (ZoomControl)LogicalTreeHelper.FindLogicalNode(grid, "DlkZoomCtrl");
                Debug.Assert(zoomctrl != null);
                var area = (DlkGraphArea)LogicalTreeHelper.FindLogicalNode(grid, "DlkGraphArea");
                Debug.Assert(area != null);

                ZoomControl.SetViewFinderVisibility(zoomctrl, Visibility.Visible);
                //zoomctrl.ZoomToFill();
                //Set Fill zooming strategy so whole graph will be always visible
                DlkGraphAreaSetup(deadlocks, area);

                area.GenerateGraph(true, true);

                /* 
				 * After graph generation is finished you can apply some additional settings for newly created visual vertex and edge controls
				 * (VertexControl and EdgeControl classes).
				 * 
				 */

                //This method sets the dash style for edges. It is applied to all edges in Area.EdgesList. You can also set dash property for
                //each edge individually using EdgeControl.DashStyle property.
                //For ex.: Area.EdgesList[0].DashStyle = GraphX.EdgeDashStyle.Dash;
                SetVertexEdgeDisplay(area);

                //This method sets edges arrows visibility. It is also applied to all edges in Area.EdgesList. You can also set property for
                //each edge individually using property, for ex: Area.EdgesList[0].ShowArrows = true;
                area.ShowAllEdgesArrows(true);

                //This method sets edges labels visibility. It is also applied to all edges in Area.EdgesList. You can also set property for
                //each edge individually using property, for ex: Area.EdgesList[0].ShowLabel = true;
                area.ShowAllEdgesLabels(true);

                zoomctrl.ZoomToFill();

                area.RelayoutGraph();
                zoomctrl.ZoomToFill();

                var tab = new CloseableTabItem()
                {
                    Header = Constants.BlackDiamond + " Deadlock",
                    Content = grid,
                    Name = "GeneralInfoViewTab"
                };
                MainTab.Items.Add(tab);
                MainTab.SelectedItem = tab;
                MainTab.UpdateLayout();


                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        private void DlkGraphAreaSetup(int[][] deadlock, DlkGraphArea area)
        {
            //Lets create logic core and filled data graph with edges and vertices
            var logicCore = new DlkGXLogicCore() { Graph = DlkGraphSetup(deadlock) };
            //This property sets layout algorithm that will be used to calculate vertices positions
            //Different algorithms uses different values and some of them uses edge Weight property.
            logicCore.DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.KK;
            //Now we can set parameters for selected algorithm using AlgorithmFactory property. This property provides methods for
            //creating all available algorithms and algo parameters.
            logicCore.DefaultLayoutAlgorithmParams =
                logicCore.AlgorithmFactory.CreateLayoutParameters(LayoutAlgorithmTypeEnum.EfficientSugiyama);
            //Unfortunately to change algo parameters you need to specify params type which is different for every algorithm.
            //((KKLayoutParameters)logicCore.DefaultLayoutAlgorithmParams).MaxIterations = 100;

            //This property sets vertex overlap removal algorithm.
            //Such algorithms help to arrange vertices in the layout so no one overlaps each other.
            logicCore.DefaultOverlapRemovalAlgorithm = OverlapRemovalAlgorithmTypeEnum.FSA;
            //Default parameters are created automaticaly when new default algorithm is set and previous params were NULL
            logicCore.DefaultOverlapRemovalAlgorithmParams.HorizontalGap = 50;
            logicCore.DefaultOverlapRemovalAlgorithmParams.VerticalGap = 50;

            //This property sets edge routing algorithm that is used to build route paths according to algorithm logic.
            //For ex., SimpleER algorithm will try to set edge paths around vertices so no edge will intersect any vertex.
            //Bundling algorithm will try to tie different edges that follows same direction to a single channel making complex graphs more appealing.
            logicCore.DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.SimpleER;

            //This property sets async algorithms computation so methods like: Area.RelayoutGraph() and Area.GenerateGraph()
            //will run async with the UI thread. Completion of the specified methods can be catched by corresponding events:
            //Area.RelayoutFinished and Area.GenerateGraphFinished.
            logicCore.AsyncAlgorithmCompute = false;

            //Finally assign logic core to GraphArea object
            area.LogicCore = logicCore;
        }

        private DlkGraph DlkGraphSetup(int[][] deadlocks)
        {
            //Lets make new data graph instance
            var dataGraph = new DlkGraph();
            //Now we need to create edges and vertices to fill data graph
            //This edges and vertices will represent graph structure and connections
            //Lets make some vertices

            var vertices = new Dictionary<int,DataVertex>();
            var vertexSet = new HashSet<int>();

            for (int i = 0, icnt = deadlocks.Length; i < icnt; ++i)
            {
                for(int j = 0, jcnt = deadlocks[i].Length; j < jcnt; ++j)
                {
                    var id = deadlocks[i][j];
                    if (!vertexSet.Add(id)) continue;
                    bool isThread;
                    var label = CurrentIndex.GetThreadOrBlkLabel(id, out isThread);
                    label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                    var node = new DataVertex(label);
                    node.ID = id;
                    vertices[id] = node;
                    dataGraph.AddVertex(node);
                }

            }
            for (int i = 0, icnt = deadlocks.Length; i < icnt; ++i)
            {
                for (int j = 1, jcnt = deadlocks[i].Length; j < jcnt; ++j)
                {
                    var dataEdge = new DataEdge(vertices[deadlocks[i][j-1]], vertices[deadlocks[i][j]]);
                    dataGraph.AddEdge(dataEdge);
                }
            }

            return dataGraph;
        }

        #endregion deadlock

        #region threads/blocks

        public bool DisplayThreadBlockMap(Digraph digraph, out string error)
        {
            error = null;
            try
            {
                var grid = this.TryFindResource("ThreadBlockingGraphXGrid") as Grid;
                Debug.Assert(grid != null);
                grid.Name = DeadlockGraphGrid + "__" + Utils.GetNewID();
                grid.Tag = new Tuple<Digraph, List<int>, SubgraphDictionary, bool>(digraph, new List<int>(), new SubgraphDictionary(), true);
                var txtBlk = (TextBlock)LogicalTreeHelper.FindLogicalNode(grid, "ThreadBlockingGraphXHelp");
                Debug.Assert(txtBlk != null);
                SetGraphHelpTextBlock(txtBlk);

                UpdateThreadBlockMap(grid);

                var tab = new CloseableTabItem()
                {
                    Header = Constants.BlackDiamond + " Threads/Blocks",
                    Content = grid,
                    Name = "GeneralInfoViewTab"
                };
                MainTab.Items.Add(tab);
                MainTab.SelectedItem = tab;
                MainTab.UpdateLayout();
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }
        
        /// <summary>
        /// TODO JRD -- use ThreadBlockingObjectsGraphGrid to display MSAGL graph.
        /// </summary>
        /// <param name="digraph"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public async void DisplayThreadBlockMap2()
        {
            string error = null;
            CloseableTabItem graphTab = null;
            SetStartTaskMainWindowState("Setting up threads and blocking objects graph, please wait...");
            try
            {
                var result = await Task.Factory.StartNew(() =>
                {
                    string err = null;
                    var info = CurrentIndex.GetThreads(out err);
                    return new Tuple<string, ClrtThread[], string[], KeyValuePair<int, ulong>[]>(error, info.Item1, info.Item2, info.Item3);
                }, DumpSTAScheduler);

                if (result.Item1 != null) // error
                {
                    ShowError(result.Item1);
                    return;
                }


                Digraph digraph = CurrentIndex.ThreadBlockgraph;

                var grid = this.TryFindResource(ThreadBlockingObjectGraphGrid) as Grid;
                Debug.Assert(grid != null);
                var graphGrid = (Grid)LogicalTreeHelper.FindLogicalNode(grid, "ThreadsGraphGrid");
                Debug.Assert(graphGrid != null);
                grid.Name = GraphGrid + "__" + Utils.GetNewID();
                /*
                 ThreadBlockingObjectsGraphGrid
                    ThreadsGrid (0,0)
                        ThreadsGraphGrid (0,0)
                        splitter (1,0)
                        ThreadFrames (2,0)
                    no name grid (0,1)
                        ThreadAliveStackObjects (0,0)
                        splitter (1,0)
                        ThreadDeadStackObjects (2,0)
                 * */
                GraphViewer graphViewer = new GraphViewer();
                graphViewer.GraphCanvas.HorizontalAlignment = HorizontalAlignment.Stretch;
                graphViewer.GraphCanvas.VerticalAlignment = VerticalAlignment.Stretch;
                graphGrid.Children.Add(graphViewer.GraphCanvas);

                graphGrid.UpdateLayout();
                grid.Tag = new Tuple<GraphViewer, ClrtThread[], string[], KeyValuePair<int, ulong>[]>(graphViewer,result.Item2,result.Item3,result.Item4);

                graphTab = DisplayTab(Constants.BlackDiamondHeader, "Threads/Blocks", grid, ThreadBlockingObjectGraphGrid + "TAB");
                Graph graph = new Graph();

                Microsoft.Msagl.Layout.MDS.MdsLayoutSettings layoutAlgorithmSettings = new Microsoft.Msagl.Layout.MDS.MdsLayoutSettings();
                //Microsoft.Msagl.Layout.Layered.SugiyamaLayoutSettings layoutAlgorithmSettings = new Microsoft.Msagl.Layout.Layered.SugiyamaLayoutSettings();
                layoutAlgorithmSettings.NodeSeparation = 40.0;
                layoutAlgorithmSettings.ClusterMargin = 20;

                graph.LayoutAlgorithmSettings = layoutAlgorithmSettings;
                
                var adjLst = digraph.AdjacencyLists;
                string[] nodeNames = new string[adjLst.Length];
                Bitset threadFlags = new Bitset(adjLst.Length);
                for (int i = 0, icnt = adjLst.Length; i < icnt; ++i)
                {
                    bool isThread;
                    string nodeLabel = CurrentIndex.GetThreadOrBlkUniqueLabel(i, out isThread);
                    Node node = graph.AddNode(nodeLabel);
                    node.Attr.LabelMargin = 10;
                    node.Attr.Padding = 20;
                    if (isThread)
                    {
                        node.Attr.FillColor = Color.Chocolate;
                        threadFlags.Set(i);
                    }
                    nodeNames[i] = nodeLabel;
                }
                for (int i = 0, icnt = adjLst.Length; i < icnt; ++i)
                {
                    if (adjLst[i] == null) continue;
                    for (int j = 0, jcnt = adjLst[i].Count; j < jcnt; ++j)
                    {
                        int targetNdx = adjLst[i][j];
                        Edge edge = (Edge)graph.AddEdge(nodeNames[i], nodeNames[targetNdx]);
                        if (threadFlags.IsSet(i))
                        {
                            edge.Attr.AddStyle(Microsoft.Msagl.Drawing.Style.Dashed);
                        }
                    }
                }

                graphViewer.Graph = graph;
                //graphViewer.GraphCanvas.MouseLeftButtonUp += GraphCanvas_MouseUp;
                graphViewer.MouseUp += GraphViewer_MouseUp; ;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                graphTab?.Close();
                ShowError(error);
            }
            finally
            {
                string msg = error != null ? "Setting up threads and blocking objects graph failed." : "Setting up threads and blocking objects graph succeeded.";
                SetEndTaskMainWindowState(msg);
            }
        }

        private void GraphViewer_MouseUp(object sender, MsaglMouseEventArgs e)
        {
            var grid = GetCurrentTabGrid();
            if (grid == null) return;
            var info = grid.Tag as Tuple<GraphViewer, ClrtThread[], string[], KeyValuePair<int, ulong>[]>;
            GraphViewer graphViewer = info.Item1;
            if (graphViewer != null && graphViewer.ObjectUnderMouseCursor != null)
            {
                // TODO JRD -- use this to change graph layout
                //var graph = graphViewer.Graph;
                ////Microsoft.Msagl.Layout.MDS.MdsLayoutSettings layoutAlgorithmSettings = new Microsoft.Msagl.Layout.MDS.MdsLayoutSettings();
                //Microsoft.Msagl.Layout.Layered.SugiyamaLayoutSettings layoutAlgorithmSettings = new Microsoft.Msagl.Layout.Layered.SugiyamaLayoutSettings();
                //layoutAlgorithmSettings.NodeSeparation = 40.0;
                //layoutAlgorithmSettings.ClusterMargin = 20;
                //graph.LayoutAlgorithmSettings = layoutAlgorithmSettings;
                //graphViewer.Graph = graph;
                //graphViewer.Invalidate();

                VNode node = graphViewer.ObjectUnderMouseCursor as VNode;
                if (node != null)
                {
                    MainStatusShowMessage(node.Node.Label.Text);
                    return;
                }
            }
        }

        private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            int a = 1;
        }

        public void UpdateThreadBlockMap(Grid grid)
        {
            try
            {
                SetStartTaskMainWindowState("Generating thread/block graph, please wait...");
                var zoomctrl = (ZoomControl)LogicalTreeHelper.FindLogicalNode(grid, "TBZoomCtrl");
                Debug.Assert(zoomctrl != null);
                var area = (DlkGraphArea)LogicalTreeHelper.FindLogicalNode(grid, "TBGraphArea");
                Debug.Assert(area != null);
                area.RemoveAllEdges(true);
                area.RemoveAllVertices(true);
                var graphInfo = grid.Tag as Tuple<Digraph, List<int>, SubgraphDictionary, bool>;
                var digraph = graphInfo.Item1;
                var currentIds = graphInfo.Item2;
                var subgraphsDct = graphInfo.Item3;
                var forward = graphInfo.Item4;


                string error;
                TBGraphAreaSetup(digraph, area, currentIds, subgraphsDct, forward, out error);

                area.GenerateGraph(true, true);

                /* 
				 * After graph generation is finished you can apply some additional settings for newly created visual vertex and edge controls
				 * (VertexControl and EdgeControl classes).
				 * 
				 */

                //This method sets the dash style for edges. It is applied to all edges in Area.EdgesList. You can also set dash property for
                //each edge individually using EdgeControl.DashStyle property.
                //For ex.: Area.EdgesList[0].DashStyle = GraphX.EdgeDashStyle.Dash;
                SetVertexEdgeDisplay(area);

                //This method sets edges arrows visibility. It is also applied to all edges in Area.EdgesList. You can also set property for
                //each edge individually using property, for ex: Area.EdgesList[0].ShowArrows = true;
                area.ShowAllEdgesArrows(true);

                //This method sets edges labels visibility. It is also applied to all edges in Area.EdgesList. You can also set property for
                //each edge individually using property, for ex: Area.EdgesList[0].ShowLabel = true;
                area.ShowAllEdgesLabels(true);


                zoomctrl.ZoomToFill();
                SetEndTaskMainWindowState("Generating thread/block graph, done");
            }
            catch (Exception ex)
            {
                SetEndTaskMainWindowState("Generating thread/block graph, FAILED!");
                ShowError(Utils.GetExceptionErrorString(ex));
            }
        }

        private bool TBGraphAreaSetup(Digraph digraph, DlkGraphArea area, List<int> currentIds,
            SubgraphDictionary subgraphsDct, bool forward, out string error)
        {
            error = null;
            try
            {
                DlkGraph graph = TBGraphSetup(digraph, currentIds, subgraphsDct, forward, out error);
                if (graph == null) return false;
                //Lets create logic core and filled data graph with edges and vertices
                var logicCore = new DlkGXLogicCore() { Graph = graph };
                //This property sets layout algorithm that will be used to calculate vertices positions
                //Different algorithms uses different values and some of them uses edge Weight property.
                logicCore.DefaultLayoutAlgorithm = LayoutAlgorithmTypeEnum.KK;
                //Now we can set parameters for selected algorithm using AlgorithmFactory property. This property provides methods for
                //creating all available algorithms and algo parameters.
                logicCore.DefaultLayoutAlgorithmParams =
                    logicCore.AlgorithmFactory.CreateLayoutParameters(LayoutAlgorithmTypeEnum.Sugiyama);
                //Unfortunately to change algo parameters you need to specify params type which is different for every algorithm.
                //((KKLayoutParameters)logicCore.DefaultLayoutAlgorithmParams).MaxIterations = 100;

                //This property sets vertex overlap removal algorithm.
                //Such algorithms help to arrange vertices in the layout so no one overlaps each other.
                logicCore.DefaultOverlapRemovalAlgorithm = OverlapRemovalAlgorithmTypeEnum.FSA;
                //Default parameters are created automaticaly when new default algorithm is set and previous params were NULL
                logicCore.DefaultOverlapRemovalAlgorithmParams.HorizontalGap = 50;
                logicCore.DefaultOverlapRemovalAlgorithmParams.VerticalGap = 50;

                //This property sets edge routing algorithm that is used to build route paths according to algorithm logic.
                //For ex., SimpleER algorithm will try to set edge paths around vertices so no edge will intersect any vertex.
                //Bundling algorithm will try to tie different edges that follows same direction to a single channel making complex graphs more appealing.
                logicCore.DefaultEdgeRoutingAlgorithm = EdgeRoutingAlgorithmTypeEnum.SimpleER;

                //This property sets async algorithms computation so methods like: Area.RelayoutGraph() and Area.GenerateGraph()
                //will run async with the UI thread. Completion of the specified methods can be catched by corresponding events:
                //Area.RelayoutFinished and Area.GenerateGraphFinished.
                logicCore.AsyncAlgorithmCompute = false;

                //Finally assign logic core to GraphArea object
                area.LogicCore = logicCore;

                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
        }

        private const int MaxTBGraphEdges = 300;

        private DlkGraph TBGraphSetup(Digraph digraph,
            List<int> currentThreadIds,
            SubgraphDictionary subgraphsDct,
            bool forward,
            out string error)
        {
            error = null;
            try
            {
                if (currentThreadIds.Count > 0) // displaying subgraphs, subgraphsDct is already populated
                {
                    return TBGetSubgraph(currentThreadIds, subgraphsDct, forward, out error);
                }
                if (digraph.EdgeCount > MaxTBGraphEdges)
                {
                    TBCreateSubgraphs(digraph, subgraphsDct);
                    return TBGetSubgraph(currentThreadIds, subgraphsDct, forward, out error);
                }
                //Lets make new data graph instance
                var dataGraph = new DlkGraph();
                //Now we need to create edges and vertices to fill data graph
                //This edges and vertices will represent graph structure and connections
                //Lets make some vertices

                List<int>[] adjLists = digraph.AdjacencyLists;

                if (digraph.EdgeCount > 300)
                {
                    subgraphsDct = new SortedDictionary<int, triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>>();
                    for (int i = 0, icnt = adjLists.Length; i < icnt; ++i)
                    {
                        var lst = adjLists[i];
                        if (lst == null || lst.Count < 1) continue;
                        bool isThread = CurrentIndex.IsThreadId(i);
                        if (isThread)
                        {
                            triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int> info;
                            if (subgraphsDct.TryGetValue(i, out info))
                            {
                                info.First.Add(lst);
                                var cnt = info.Third + lst.Count;
                                subgraphsDct[i] = new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(info.First, info.Second,
                                    cnt);
                            }
                            else
                            {
                                var lists = new List<List<int>>();
                                lists.Add(lst);
                                subgraphsDct.Add(i, new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(lists, null, lst.Count));
                            }
                        }
                        else
                        {
                            for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
                            {
                                var id = lst[j];
                                if (!CurrentIndex.IsThreadId(id)) continue;

                                triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int> info;
                                if (subgraphsDct.TryGetValue(id, out info))
                                {
                                    var lists = info.Second;
                                    if (lists == null)
                                    {
                                        lists = new List<KeyValuePair<List<int>, int>>();
                                    }
                                    lists.Add(new KeyValuePair<List<int>, int>(lst, i));
                                    var cnt = info.Third + lst.Count;
                                    subgraphsDct[id] = new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(info.First, lists, cnt);
                                }
                                else
                                {
                                    var lists = new List<KeyValuePair<List<int>, int>>();
                                    lists.Add(new KeyValuePair<List<int>, int>(lst, i));
                                    subgraphsDct.Add(id,
                                        new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(info.First, lists, lst.Count));
                                }
                            }
                        }

                    }
                }

                DataVertex[] vertices = new DataVertex[digraph.VertexCount];
                HashSet<int> set = new HashSet<int>();

                for (int i = 0, icnt = adjLists.Length; i < icnt; ++i)
                {
                    if (adjLists[i] == null || adjLists[i].Count < 1) continue;
                    DataVertex node = vertices[i];
                    if (node == null)
                    {
                        bool isThread;
                        var label = CurrentIndex.GetThreadOrBlkLabel(i, out isThread);
                        label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                        node = new DataVertex(label);
                        node.ID = i;
                        vertices[i] = node;
                        dataGraph.AddVertex(node);
                    }
                    var lst = adjLists[i];
                    for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
                    {
                        var id = lst[j];
                        DataVertex aNode = vertices[id];
                        if (aNode == null)
                        {
                            bool isThread;
                            var label = CurrentIndex.GetThreadOrBlkLabel(id, out isThread);
                            label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                            aNode = new DataVertex(label);
                            aNode.ID = id;
                            vertices[id] = aNode;
                            dataGraph.AddVertex(aNode);
                        }
                        var dataEdge = new DataEdge(node, aNode);
                        dataGraph.AddEdge(dataEdge);
                    }
                }

                return dataGraph;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }

        private DlkGraph TBGetSubgraph(List<int> currentThreadIds,
                                        SubgraphDictionary subgraphsDct,
                                        bool forward,
                                        out string error)
        {
            error = null;
            try
            {
                List<int> threadIds = new List<int>();
                var dataGraph = new DlkGraph();
                var nodes = new Dictionary<int, DataVertex>();
                var ecount = 0;
                var lastId = currentThreadIds.Count > 0 ? currentThreadIds[currentThreadIds.Count - 1] : Int32.MaxValue;
                Stack<KeyValuePair<int, int>> stack = forward ? null : new Stack<KeyValuePair<int, int>>(subgraphsDct.Count / 2);
                foreach (var kv in subgraphsDct)
                {
                    if (lastId == Int32.MaxValue)
                    {
                        threadIds.Add(kv.Key);
                        ecount += kv.Value.Third;
                    }
                    else if (!forward)
                    {
                        var id = kv.Key;
                        if (id == currentThreadIds[0])
                        {
                            break;
                        }
                        stack.Push(new KeyValuePair<int, int>(id, kv.Value.Third));
                    }
                    else
                    {
                        var id = kv.Key;
                        if (id > lastId)
                        {
                            threadIds.Add(id);
                            ecount += kv.Value.Third;
                        }
                    }
                    if (ecount > MaxTBGraphEdges)
                        break;
                }
                if (!forward)
                {
                    while (stack.Count > 0 && ecount <= MaxTBGraphEdges)
                    {
                        var kv = stack.Pop();
                        threadIds.Add(kv.Key);
                        ecount += kv.Value;
                    }
                }

                foreach (var thrdId in threadIds)
                {
                    var triple = subgraphsDct[thrdId];
                    var lists1 = triple.First;
                    var lists2 = triple.Second;
                    DataVertex node = null;
                    if (!nodes.TryGetValue(thrdId, out node))
                    {
                        bool isThread;
                        var label = CurrentIndex.GetThreadOrBlkLabel(thrdId, out isThread);
                        label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                        node = new DataVertex(label);
                        node.ID = thrdId;
                        dataGraph.AddVertex(node);
                        nodes.Add(thrdId, node);
                    }

                    if (lists1 != null)
                    {
                        for (int i = 0, icnt = lists1.Count; i < icnt; ++i)
                        {
                            var lst = lists1[i];
                            for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
                            {
                                var aid = lst[j];
                                DataVertex anode = null;
                                if (!nodes.TryGetValue(aid, out anode))
                                {
                                    bool isThread;
                                    var label = CurrentIndex.GetThreadOrBlkLabel(aid, out isThread);
                                    label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                                    anode = new DataVertex(label);
                                    anode.ID = aid;
                                    dataGraph.AddVertex(anode);
                                    nodes.Add(aid, anode);
                                }
                                var dataEdge = new DataEdge(node, anode);
                                dataGraph.AddEdge(dataEdge);

                            }
                        }
                    }

                    if (lists2 != null)
                    {
                        for (int i = 0, icnt = lists2.Count; i < icnt; ++i)
                        {
                            var id = lists2[i].Value;
                            var lst = lists2[i].Key;

                            node = null;
                            if (!nodes.TryGetValue(id, out node))
                            {
                                bool isThread;
                                var label = CurrentIndex.GetThreadOrBlkLabel(id, out isThread);
                                label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                                node = new DataVertex(label);
                                node.ID = id;
                                dataGraph.AddVertex(node);
                                nodes.Add(id, node);
                            }

                            for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
                            {
                                var aid = lst[j];
                                DataVertex anode = null;
                                if (!nodes.TryGetValue(aid, out anode))
                                {
                                    bool isThread;
                                    var label = CurrentIndex.GetThreadOrBlkLabel(aid, out isThread);
                                    label = (isThread ? Constants.HeavyRightArrowHeader : Constants.BlackFourPointedStarHeader) + label;
                                    anode = new DataVertex(label);
                                    anode.ID = aid;
                                    dataGraph.AddVertex(anode);
                                    nodes.Add(aid, anode);
                                }
                                var dataEdge = new DataEdge(node, anode);
                                dataGraph.AddEdge(dataEdge);
                            }
                        }
                    }
                }

                currentThreadIds.Clear();
                currentThreadIds.AddRange(threadIds);
                return dataGraph;

            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
        }


        private void TBCreateSubgraphs(Digraph digraph, SubgraphDictionary subgraphsDct)
        {
            Debug.Assert(subgraphsDct != null && subgraphsDct.Count == 0);
            List<int>[] adjLists = digraph.AdjacencyLists;
            for (int i = 0, icnt = adjLists.Length; i < icnt; ++i)
            {
                var lst = adjLists[i];
                if (lst == null || lst.Count < 1) continue;
                bool isThread = CurrentIndex.IsThreadId(i);
                if (isThread)
                {
                    triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int> info;
                    if (subgraphsDct.TryGetValue(i, out info))
                    {
                        info.First.Add(lst);
                        var cnt = info.Third + lst.Count;
                        subgraphsDct[i] = new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(info.First, info.Second, cnt);
                    }
                    else
                    {
                        var lists = new List<List<int>>();
                        lists.Add(lst);
                        subgraphsDct.Add(i, new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(lists, null, lst.Count));
                    }
                }
                else
                {
                    for (int j = 0, jcnt = lst.Count; j < jcnt; ++j)
                    {
                        var id = lst[j];
                        if (!CurrentIndex.IsThreadId(id)) continue;

                        triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int> info;
                        if (subgraphsDct.TryGetValue(id, out info))
                        {
                            var lists = info.Second;
                            if (lists == null)
                            {
                                lists = new List<KeyValuePair<List<int>, int>>();
                            }
                            lists.Add(new KeyValuePair<List<int>, int>(lst, i));
                            var cnt = info.Third + lst.Count;
                            subgraphsDct[id] = new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(info.First, lists, cnt);
                        }
                        else
                        {
                            var lists = new List<KeyValuePair<List<int>, int>>();
                            lists.Add(new KeyValuePair<List<int>, int>(lst, i));
                            subgraphsDct.Add(id, new triple<List<List<int>>, List<KeyValuePair<List<int>, int>>, int>(info.First, lists, lst.Count));
                        }
                    }
                }
            }
        }

        private void ThreadBlockingGraphXBackClicked(object sender, RoutedEventArgs e)
        {
            var mainGrid = GetCurrentTabGrid();
            Debug.Assert(mainGrid != null);

        }

        private void ThreadBlockingGraphXForwardClicked(object sender, RoutedEventArgs e)
        {
            var grid = GetCurrentTabGrid();
            Debug.Assert(grid != null);
            var graphInfo = grid.Tag as Tuple<Digraph, List<int>, SubgraphDictionary, bool>;
            var currentIds = graphInfo.Item2;
            var subgraphsDct = graphInfo.Item3;
            var forward = graphInfo.Item4;
            int lastId = currentIds != null && currentIds.Count > 0 ? currentIds[currentIds.Count - 1] : Int32.MaxValue;
            int lastAvailId = subgraphsDct == null || subgraphsDct.Count < 1 ? Int32.MaxValue : subgraphsDct.Last().Key;
            if (lastAvailId <= lastId) return;
            if (!forward)
                grid.Tag = new Tuple<Digraph, List<int>, SubgraphDictionary, bool>(graphInfo.Item1, graphInfo.Item2, graphInfo.Item3,
                    true);
            UpdateThreadBlockMap(grid);
        }

        #endregion  threads/blocks

        #region common

        private void SetVertexEdgeDisplay(DlkGraphArea area)
        {
            var dct = area.EdgesList;
            foreach (var edge in dct)
            {
                var id = edge.Key.Source.ID;
                if (CurrentIndex.IsThreadId((int)id))
                {
                    edge.Value.DashStyle = EdgeDashStyle.Dash;
                }
                else
                {
                    edge.Value.DashStyle = EdgeDashStyle.Solid;
                }
            }

            var vdct = area.VertexList;
            foreach (var vertex in vdct)
            {
                var id = vertex.Key.ID;
                if (CurrentIndex.IsThreadId((int)id))
                {
                    vertex.Value.Background = Brushes.Chocolate;
                }
            }
        }

        private void SetGraphHelpTextBlock(TextBlock txtBlk)
        {
            txtBlk.Inlines.Add(new Run(" thread ") { Background = Brushes.Chocolate });
            //txtBlk.Inlines.Add(new Bold(new Run("-->")) { FontSize = 18 });
            txtBlk.Inlines.Add(new Run(Constants.RightDashedArrowPadded) { FontSize = 18 });
            txtBlk.Inlines.Add(new Run(" blocking obj ") { Background = Brushes.LightGray });
            txtBlk.Inlines.Add(new Run(" thread waits for blocking obj    "));

            txtBlk.Inlines.Add(new Run(" blocking obj ") { Background = Brushes.LightGray });
            //txtBlk.Inlines.Add(new Bold(new Run(Constants.RightSolidArrowPadded)) { FontSize = 16 });
            txtBlk.Inlines.Add(new Run(Constants.RightSolidArrowPadded) { FontSize = 18 });
            txtBlk.Inlines.Add(new Run(" os/mngd ids ") { Background = Brushes.Chocolate });
            txtBlk.Inlines.Add(new Run(" thread owns blocking obj  "));
        }

        #endregion common

        #endregion graphs

        #region Display Grids

        public CloseableTabItem DisplayTab(char prefix, string reportTitle, Grid grid, string name)
        {
            var tab = new CloseableTabItem() { Header = prefix + " " + reportTitle, Content = grid, Name = name + "__" + Utils.GetNewID() };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            return tab;
        }

        public CloseableTabItem DisplayTab(string prefix, string reportTitle, Grid grid, string name)
        {
            var tab = new CloseableTabItem() { Header = prefix + reportTitle, Content = grid, Name = name + "__" + Utils.GetNewID() };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            return tab;
        }

        /// <summary>
        /// Show information about the crash dump.
        /// We have this stored in a text file, in a index folder, with postfix : ~INDEXIFO.txt.
        /// </summary>
        /// <param name="info">General crush dump info.</param>
        private void DisplayGeneralInfoGrid(string info)
        {
            Debug.Assert(CurrentIndex != null);
            var grid = this.TryFindResource("GeneralInfoView") as Grid;
            Debug.Assert(grid != null);

            var txtBlock = (TextBlock)LogicalTreeHelper.FindLogicalNode(grid, "GeneralInfoText");
            Debug.Assert(txtBlock != null);

            if (CurrentIndex.DeadlockFound)
            {
                txtBlock.Inlines.Add(Environment.NewLine);
                txtBlock.Inlines.Add(new Run("POSSIBLE THREAD DEADLOCK FOUND!") { FontWeight = FontWeights.Bold, FontSize = 12, Foreground = Brushes.Red });
                txtBlock.Inlines.Add(new Run(" see threads/blocking objects graph.") { FontWeight = FontWeights.Bold, FontSize = 10, FontStyle = FontStyles.Italic });
                txtBlock.Inlines.Add(Environment.NewLine);
                txtBlock.Inlines.Add(Environment.NewLine);
            }

            var lines = info.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0, icnt = lines.Length; i < icnt; ++i)
            {
                var ln = lines[i];
                if (ln.StartsWith("RUNTIME"))
                {
                    txtBlock.Inlines.Add(Environment.NewLine);
                    txtBlock.Inlines.Add(new Run(ln) { FontWeight = FontWeights.Bold, FontSize = 16, });
                    txtBlock.Inlines.Add(Environment.NewLine);
                    continue;
                }
                var pos = ln.IndexOf(':');
                if (pos > 0)
                {
                    txtBlock.Inlines.Add(new Run(ln.Substring(0, pos + 1)) { FontWeight = FontWeights.Bold, FontSize = 12 });
                    txtBlock.Inlines.Add("   " + ln.Substring(pos + 2));
                    txtBlock.Inlines.Add(Environment.NewLine);
                }
                else
                {
                    txtBlock.Inlines.Add(ln);
                    txtBlock.Inlines.Add(Environment.NewLine);
                }
            }

            txtBlock.Inlines.Add(Environment.NewLine);

            string error;
            var genDistributions = CurrentIndex.GetTotalGenerationDistributions(out error);
            if (error != null)
            {
                txtBlock.Inlines.Add(new Run("FAILED TO LOAD GENERATION DISTRIBUTIONS!!!" + Environment.NewLine) { FontWeight = FontWeights.Bold, FontSize = 12 });
                txtBlock.Inlines.Add(new Run(error + Environment.NewLine) { FontWeight = FontWeights.Bold, FontSize = 10, Foreground = Brushes.Red });
            }
            else
            {
                DisplayableGeneration[] generations = new DisplayableGeneration[6];
                generations[0] = new DisplayableGeneration("Object Counts", genDistributions.Item1);
                generations[1] = new DisplayableGeneration("Object Sizes", genDistributions.Item2);
                generations[2] = new DisplayableGeneration("Unrooted Counts", genDistributions.Item5);
                generations[3] = new DisplayableGeneration("Unrooted Sizes", genDistributions.Item6);
                generations[4] = new DisplayableGeneration("Free Counts", genDistributions.Item3);
                generations[5] = new DisplayableGeneration("Free Sizes", genDistributions.Item4);

                var genDataGrid = (DataGrid)LogicalTreeHelper.FindLogicalNode(grid, "GeneralInfoGenerations");
                Debug.Assert(genDataGrid != null);
                genDataGrid.ItemsSource = generations;

                // display generation charts
                //
                //var chartGrid = (Grid)LogicalTreeHelper.FindLogicalNode(grid, "GeneralInfoChart");
                //Debug.Assert(chartGrid != null);

                //var grid1 = (Grid)LogicalTreeHelper.FindLogicalNode(chartGrid, "GeneralInfoChart1");
                //Debug.Assert(grid1 != null);
                //var grid2 = (Grid)LogicalTreeHelper.FindLogicalNode(chartGrid, "GeneralInfoChart2");
                //Debug.Assert(grid2 != null);

                //System.Windows.Forms.Integration.WindowsFormsHost host0 = new System.Windows.Forms.Integration.WindowsFormsHost();
                //host0.FontSize = 8;
                //List<int> intLst = new List<int>(genDistributions.Item1.Length + genDistributions.Item5.Length + genDistributions.Item3.Length);
                //intLst.AddRange(genDistributions.Item1); intLst.AddRange(genDistributions.Item5); intLst.AddRange(genDistributions.Item3);
                //host0.Child = DmpNdxQueries.Auxiliaries.getCountGenerationsChart2(intLst.ToArray());
                //host0.Child.Font = new Font("Arial", 8);
                //System.Windows.Forms.Integration.WindowsFormsHost host1 = new System.Windows.Forms.Integration.WindowsFormsHost();
                //host1.FontSize = 8;
                //List<ulong> ulongLst = new List<ulong>(genDistributions.Item2.Length + genDistributions.Item6.Length + genDistributions.Item4.Length);
                //ulongLst.AddRange(genDistributions.Item2); ulongLst.AddRange(genDistributions.Item6); ulongLst.AddRange(genDistributions.Item4);
                //host1.Child = DmpNdxQueries.Auxiliaries.getSizeGenerationsChart2(ulongLst.ToArray());
                //host1.Child.Font = new Font("Arial", 8);
                //grid1.Children.Add(host0);
                //grid2.Children.Add(host1);
            }

            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " General Info", Content = grid, Name = "GeneralInfoViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
        }

        private void DisplayGenerationLine(TextBlock txtBlock, string[] titles, ulong[] values)
        {
            Debug.Assert(titles.Length == values.Length);
            ulong total = 0UL;
            for (int i = 0, icnt = titles.Length; i < icnt; ++i)
            {
                total += values[i];
                txtBlock.Inlines.Add(new Run("  " + titles[i] + ": [" + Utils.LargeNumberString(values[i]) + "]"));
            }
            txtBlock.Inlines.Add(new Run("    Total Size: " + Utils.LargeNumberString(total)));
        }

        #region types main displays

        private void DisplayNamespaceGrid(KeyValuePair<string, KeyValuePair<string, int>[]>[] namespaces)
        {
            var grid = this.TryFindResource(NameNamespaceTypeNameGrid) as Grid;
            Debug.Assert(grid != null);
            grid.Name = NameNamespaceTypeNameGrid + "__" + Utils.GetNewID();
            grid.Tag = namespaces;
            var lb = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "lbTpNamespaces");
            Debug.Assert(lb != null);
            lb.ItemsSource = namespaces;
            var nsCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpNsCount");
            Debug.Assert(nsCountLabel != null);
            nsCountLabel.Content = Utils.LargeNumberString(namespaces.Length);
            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Instances " + Constants.CircledOneChar, Content = grid, Name = "HeapIndexTypeViewTab" };
            var addressList = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNamespaceAddresses");
            Debug.Assert(addressList != null);
            addressList.ContextMenu.Tag = addressList;
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            lb.SelectedIndex = 0;
        }

        private void lbTpNamespacesSelectionChange(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(CurrentIndex != null);
            var grid = GetCurrentTabGrid();
            var lb = sender as ListBox;
            var lbNamespaceViewTypeNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbNamespaceViewTypeNames");
            var selndx = lb.SelectedIndex;
            if (selndx < 0) return;
            var data = lb.ItemsSource as KeyValuePair<string, KeyValuePair<string, int>[]>[];
            Debug.Assert(lbNamespaceViewTypeNames != null);
            var classCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpClassCount");
            lbNamespaceViewTypeNames.ItemsSource = data[selndx].Value;
            lbNamespaceViewTypeNames.SelectedIndex = 0;
            classCountLabel.Content = Utils.LargeNumberString(data[selndx].Value.Length);
        }

        private void lbTpNamesSelectionChange(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(CurrentIndex != null);
            var grid = GetCurrentTabGrid();
            var lbNamespaceViewTypeNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbNamespaceViewTypeNames");
            Debug.Assert(lbNamespaceViewTypeNames != null);
            var selndx = lbNamespaceViewTypeNames.SelectedIndex;
            if (selndx < 0) return;

            var data = lbNamespaceViewTypeNames.ItemsSource as KeyValuePair<string, int>[];
            int unrootedCount;
            var addresses = CurrentIndex.GetTypeInstances(data[selndx].Value, out unrootedCount);
            var lbTpNsTypeInfo = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lbTpNsTypeInfo");
            Debug.Assert(lbTpNsTypeInfo != null);
            lbTpNsTypeInfo.Content = data[selndx].Value.ToString();
            var lbTpAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNamespaceAddresses");
            Debug.Assert(lbTpAddresses != null);
            var addrCountLabel = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lTpAddressCount");
            Debug.Assert(addrCountLabel != null);
            addrCountLabel.Content = Utils.CountString(addresses.Length)
                + (unrootedCount > 0 ? (", unrooted: " + Utils.CountString(unrootedCount)) : string.Empty);
            lbTpAddresses.ItemsSource = addresses;

        }

        private void DisplayTypesGrid(bool reversedTypeNames)
        {
            Debug.Assert(CurrentIndex != null);
            var grid = this.TryFindResource(GridKeyNameTypeView) as Grid;
            Debug.Assert(grid != null);
            grid.Name = (reversedTypeNames ? GridReversedNameTypeView : GridNameTypeView) + "__" + Utils.GetNewID();
            var lb = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "lbTypeViewTypeNames");
            Debug.Assert(lb != null);
            if (reversedTypeNames)
                lb.ItemsSource = CurrentIndex.ReversedTypeNames;
            else
                lb.ItemsSource = CurrentIndex.DisplayableTypeNames;
            var lab = (Label)LogicalTreeHelper.FindLogicalNode(grid, "lTypeCount");
            Debug.Assert(lab != null);
            lab.Content = "type count: " + string.Format("{0:#,###}", CurrentIndex.UsedTypeCount);

            var addressList = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNameAddresses");
            Debug.Assert(addressList != null);
            addressList.ContextMenu.Tag = addressList;

            string title = reversedTypeNames ? (" Instances " + Constants.CircledTwoChar) : (" Instances " + Constants.CircledThreeChar);
            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + title, Content = grid, Name = "HeapIndexTypeViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
        }

        private void lbTypeNamesSelectionChange(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(CurrentIndex != null);
            Debug.Assert(sender is ListBox);
            var grid = ((ListBox)sender).Parent as Grid;
            var lbNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeViewTypeNames");
            Debug.Assert(lbNames != null);
            var lbAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"lbTypeNameAddresses");
            Debug.Assert(lbAddresses != null);
            var selndx = lbNames.SelectedIndex;
            if (selndx < 0) return;
            var data = lbNames.ItemsSource as KeyValuePair<string, int>[];
            int unrootedCount;
            var addresses = CurrentIndex.GetTypeInstances(data[selndx].Value, out unrootedCount);
            var lab = (Label)LogicalTreeHelper.FindLogicalNode(grid, @"lAddressCount");
            Debug.Assert(lab != null);
            lab.Content = Utils.CountString(addresses.Length)
                + (unrootedCount > 0 ? (", unrooted: " + Utils.CountString(unrootedCount)) : string.Empty);
            lbAddresses.ItemsSource = addresses;
            lbAddresses.SelectedIndex = 0;
        }

        private ListBox GetTypeNamesListBox(object sender)
        {
            var grid = GetCurrentTabGrid();
            string listName = grid.Name.StartsWith(NameNamespaceTypeNameGrid, StringComparison.Ordinal) ? "lbNamespaceViewTypeNames" : "lbTypeViewTypeNames";
            var lbNames = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, listName);
            Debug.Assert(lbNames != null);
            return lbNames;
        }

        private string GetTypeNameFromSelection(object sel, out int typeId)
        {
            typeId = Constants.InvalidIndex;
            string typeName = string.Empty;
            if (sel is KeyValuePair<string, int>)
            {
                KeyValuePair<string, int> kv = (KeyValuePair<string, int>)sel;
                typeName = CurrentIndex.GetTypeName(kv.Value);
                typeId = kv.Value;
            }
            else
            {
                typeName = sel.ToString();
            }
            return typeName;
        }

        private KeyValuePair<string, int> GetTypeNameInfo(object obj)
        {
            Debug.Assert(obj is KeyValuePair<string, int>);
            return (KeyValuePair<string, int>)obj;
        }

        private bool GetTypeNameInfo(object sender, out string typeName, out int typeId)
        {
            typeName = null;
            typeId = Constants.InvalidIndex;
            var lbNames = GetTypeNamesListBox(sender);
            if (lbNames == null || lbNames.SelectedItems.Count < 1)
            {
                MessageBox.Show("No type is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            var kv = GetTypeNameInfo(lbNames.SelectedItems[0]);
            typeName = kv.Key;
            typeId = kv.Value;
            return true;
        }

        private void CopyTypeNameClicked(object sender, RoutedEventArgs e)
        {
            string typeName;
            int typeId;
            if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
            typeName = CurrentIndex.GetTypeName(typeId);
            GuiUtils.CopyToClipboard(typeName);
            MainStatusShowMessage("Copied to Clipboard: " + typeName);
        }

        private void GenerationDistributionClicked(object sender, RoutedEventArgs e)
        {
            string typeName;
            int typeId;
            if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
            var genHistogram = CurrentIndex.GetTypeGcGenerationHistogram(typeId);
            var histStr = ClrtSegment.GetGenerationHistogramSimpleString(genHistogram);
            MainStatusShowMessage(typeName + ": " + histStr);
        }

        private void GetFieldValuesClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Not implemented yet.", "Get Field Values", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private async void GetFieldDefaultValuesClicked(object sender, RoutedEventArgs e)
        {
            string typeName;
            int typeId;
            if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
            SetStartTaskMainWindowState("generating type field default values report, please wait...");

            //var result = await Task.Run(() =>
            //{
            //    return CurrentIndex.GetTypeFieldDefaultValues(typeId);
            //});

            var result = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetTypeFieldDefaultValues(typeId);
            }, DumpSTAScheduler);


            string msg;
            if (result.Error != null)
            {
                msg = "getting default values for: " + Utils.BaseTypeName(typeName) + ", failed.";
                MessageBox.Show(result.Error, msg, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                msg = "getting default values for: " + Utils.BaseTypeName(typeName) + ", done.";
            }
            SetEndTaskMainWindowState(msg);

            DisplayListingGrid(result, Constants.BlackDiamondHeader, ListingGrid + "__TypeDefaults", "TypeDefaultValues");
        }


        //private async void GetInstancesHeapSizesClicked(object sender, bool baseSizes)
        //{
        //	string typeName;
        //	int typeId;
        //	if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
        //	SetStartTaskMainWindowState("getting type sizes, please wait...");
        //	var result = await Task.Run(() =>
        //	{
        //		string error;
        //		var tuple = CurrentIndex.GetTypeSizes(typeId, out error);
        //		return new Tuple<string, ulong, ulong>(error, tuple.Key, tuple.Value);
        //	});
        //	string msg;
        //	if (result.Item1 != null)
        //	{
        //		msg = "getting type sizes for: " + Utils.BaseTypeName(typeName) + " failed.";
        //		MessageBox.Show(result.Item1, msg, MessageBoxButton.OK, MessageBoxImage.Error);
        //	}
        //	else
        //	{
        //		msg = Utils.BaseTypeName(typeName) + " base size: " + Utils.LargeNumberString(result.Item2)
        //			+ ", size: " + Utils.LargeNumberString(result.Item3);
        //	}
        //	SetEndTaskMainWindowState(msg);
        //}

        //private void GetTotalHeapSizeClicked(object sender, RoutedEventArgs e)
        //{
        //	GetInstancesHeapSizesClicked(sender, false);
        //}

        //private void GetTotalHeapSizeReportClicked(object sender, RoutedEventArgs e)
        //{
        //	MessageBox.Show("Not implemented yet.", "Get Type Total Heap Size Report", MessageBoxButton.OK, MessageBoxImage.Information);
        //}

        private async void GetTypeSizesClicked(object sender, RoutedEventArgs e)
        {
            string typeName;
            int typeId;
            if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;
            SetStartTaskMainWindowState("getting type sizes, please wait...");
            //var result = await Task.Run(() =>
            //{
            //    string error;
            //    var tuple = CurrentIndex.GetTypeSizes(typeId, out error);
            //    return new Tuple<string, ulong, ulong>(error, tuple.Key, tuple.Value);
            //});

            var result = await Task.Factory.StartNew(() =>
            {
                string error;
                var tuple = CurrentIndex.GetTypeSizes(typeId, out error);
                return new Tuple<string, ulong, ulong>(error, tuple.Key, tuple.Value);
            }, DumpSTAScheduler);

            string msg;
            if (result.Item1 != null)
            {
                msg = "getting type sizes for: " + Utils.BaseTypeName(typeName) + " failed.";
                MessageBox.Show(result.Item1, msg, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                msg = Utils.BaseTypeName(typeName) + " base size: " + Utils.LargeNumberString(result.Item2)
                    + ", size: " + Utils.LargeNumberString(result.Item3);
            }
            SetEndTaskMainWindowState(msg);
        }

        private async void GetParentReferences(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("No index is loaded")) return;
            if (!AreReferencesAvailable("There are no references indexed")) return;
            string typeName;
            int typeId;
            if (!GetTypeNameInfo(sender, out typeName, out typeId)) return;

            int instCount = CurrentIndex.GetTypeInstanceCount(typeId);

            string description = "Get references of: " + Environment.NewLine + typeName + Environment.NewLine +
                                 "Instance count: " + instCount;

            ReferenceSearchSetup dlg = new ReferenceSearchSetup(description) { Owner = this };
            dlg.ShowDialog();
            if (dlg.Cancelled) return;
            int level = dlg.GetAllReferences ? Int32.MaxValue : dlg.SearchDepthLevel;
            var dispMode = dlg.DisplayMode;
            var searchFlag = dlg.DataSource | dlg.Direction | dlg.Strict;

            if (dispMode == ReferenceSearchSetup.DispMode.List)
            {
                SetStartTaskMainWindowState("Getting parent references for: '" + typeName + "', please wait...");

                //var report = await Task.Run(() => CurrentIndex.GetTypeReferenceReport(typeId, searchFlag, level));
                var report = await Task.Factory.StartNew(() =>
                {
                    return CurrentIndex.GetTypeReferenceReport(typeId, searchFlag, level);
                }, DumpSTAScheduler);

                if (report.Error != null)
                {
                    MessageBox.Show(report.Error, "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    SetEndTaskMainWindowState("Getting parent references for: '" + Utils.BaseTypeName(typeName) + "', failed.");
                    return;
                }
                SetEndTaskMainWindowState("Getting parent references for: '" + Utils.BaseTypeName(typeName) + "', done.");
                //DisplayListViewBottomGrid(report, Constants.BlackDiamond, ReportNameInstRef, ReportTitleInstRef);
                DisplayListingGrid(report, Constants.BlackDiamondHeader, ReportNameInstRef, ReportTitleInstRef);
            }
            if (dispMode == ReferenceSearchSetup.DispMode.Tree)
            {
                SetStartTaskMainWindowState("Getting parent references for: '" + typeName + "', please wait...");

                //var report = await Task.Run(() => CurrentIndex.GetParentTree(typeId, level));
                var report = await Task.Factory.StartNew(() =>
                {
                    return CurrentIndex.GetParentTree(typeId, level);
                }, DumpSTAScheduler);

                if (report.Item1 != null)
                {
                    MessageBox.Show(report.Item1, "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    SetEndTaskMainWindowState("Getting parent references for: '" + Utils.BaseTypeName(typeName) + "', failed.");
                    return;
                }
                SetEndTaskMainWindowState("Getting parent references for: '" + Utils.BaseTypeName(typeName) + "', done.");
                DisplayTypeAncestorsGrid(report.Item2);
            }

        }

        /// <summary>
        /// Get parents of a selected single instance.
        /// </summary>
        /// <param name="sender">Delegate invoker.</param>
        /// <param name="e">Delegate argument, not used.</param>
        private async void LbInstanceParentsClicked(object sender, RoutedEventArgs e)
        {
            if (!IsIndexAvailable("No index is loaded")) return;

            // get the address
            //
            var lbAddresses = GetTypeAddressesListBox(sender);
            if (lbAddresses.SelectedItems.Count < 1)
            {
                MessageBox.Show("No address is selected!", "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            var addr = (ulong)lbAddresses.SelectedItems[0];

            DisplayInstanceParentReferences(addr);
        }

        private async void ExecuteReferenceQuery(ulong addr)
        {
            DisplayInstanceParentReferences(addr);
        }

        private async void DisplayInstanceParentReferences(ulong addr)
        {
            if (!AreReferencesAvailable("There are no references indexed")) return;
            // get reference search info
            //
            ReferenceSearchSetup dlg = new ReferenceSearchSetup("Get parents of instance: " + Utils.RealAddressString(addr)) { Owner = this };
            dlg.ShowDialog();
            if (dlg.Cancelled) return;
            int level = dlg.GetAllReferences ? Int32.MaxValue : dlg.SearchDepthLevel;
            var dispMode = dlg.DisplayMode;
            var direction = dlg.Direction;
            var dataSource = dlg.DataSource;

            string msg = "Getting parent references for: '" + Utils.RealAddressString(addr) + "', ";
            if (dispMode == ReferenceSearchSetup.DispMode.List)
            {
                SetStartTaskMainWindowState(msg + "please wait...");

                //var report = await Task.Run(() => CurrentIndex.GetParentReferencesReport(addr, level));
                var report = await Task.Factory.StartNew(() =>
                {
                    return CurrentIndex.GetParentReferencesReport(addr, level);
                }, DumpSTAScheduler);

                if (report.Error != null)
                {
                    SetEndTaskMainWindowState(msg + "failed.");
                    MessageBox.Show(report.Error, "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                SetEndTaskMainWindowState(msg + "done.");
                //DisplayListViewBottomGrid(report, Constants.BlackDiamond, ReportNameInstRef, ReportTitleInstRef);
                DisplayListingGrid(report, Constants.BlackDiamondHeader, ReportNameInstRef, ReportTitleInstRef);

            }
            if (dispMode == ReferenceSearchSetup.DispMode.Tree)
            {
                SetStartTaskMainWindowState(msg + "please wait...");

                //(string error, AncestorNode node) = await Task.Run(() => CurrentIndex.GetParentTree(addr, level));
                (string error, AncestorNode node) = await Task.Factory.StartNew(() =>
                {
                    return CurrentIndex.GetParentTree(addr, level);
                }, DumpSTAScheduler);

                if (error != null)
                {
                    SetEndTaskMainWindowState(msg + "failed.");
                    MessageBox.Show(error, "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                SetEndTaskMainWindowState(msg + "done.");
                DisplayTypeAncestorsGrid(node);
            }
        }

        #endregion types main displays

        #region roots

        private void DisplayRoots(ClrtRoot[][] roots, ListingInfo[] listings)
        {
            Debug.Assert(CurrentIndex != null);
            var grid = this.TryFindResource(RootsGrid) as Grid;
            grid.Name = RootsGrid + "__" + Utils.GetNewID();
            Debug.Assert(grid != null);
            grid.Tag = new Tuple<ClrtRoot[][], ListingInfo[]>(roots, listings);
            var rootKindList = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, RootsGrid_RootTypeList);
            Debug.Assert(rootKindList != null);

            for (int i = 0, icnt = roots.Length; i < icnt; ++i)
            {
                var rts = roots[i];
                var cnt = rts == null ? 0 : rts.Length;
                TextBlock txtBlk = GetRoootKindText((GCRootKind)i, cnt);
                var lstItem = new ListBoxItem();
                lstItem.Content = txtBlk;
                lstItem.BorderThickness = new Thickness(1.0);
                lstItem.BorderBrush = Brushes.Gray;
                rootKindList.Items.Add(lstItem);
            }

            (Grid lstGrid, ListView lstView) = GetListingGrid("LstRoots", null, listings[0].ColInfos, new RoutedEventHandler(RootListingListViewClick));
            lstGrid.SetValue(Grid.ColumnProperty, 1);
            grid.Children.Add(lstGrid);
            DisplayTab(Constants.BlackDiamondHeader, "Roots", grid, "RootsTab");
            rootKindList.SelectedIndex = 0;
        }

        private void RootTypeListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lb = sender as ListBox;
            Debug.Assert(lb != null);
            int selNdx = lb.SelectedIndex;
            if (selNdx < 0) return;
            // we need listing grid and its list view
            Debug.Assert(lb.Parent != null && lb.Parent is Grid);
            Grid grid = lb.Parent as Grid;
            Tuple<ClrtRoot[][], ListingInfo[]> data = grid.Tag as Tuple<ClrtRoot[][], ListingInfo[]>; 
            Grid lstGrid=null;
            ListView lstView=null;
            TextBox txtBox = null;
            foreach(var child in grid.Children)
            {
                if (child is Grid) { lstGrid = (Grid)child; break; }
            }
            Debug.Assert(lstGrid != null);
            foreach (var child in lstGrid.Children)
            {
                if (child is ListView) lstView = (ListView)child;
                else if (child is TextBox) txtBox = (TextBox)child;
            }
            Debug.Assert(lstView != null);

            string reportTitle = "Roots." + ((GCRootKind)selNdx).ToString();

            PopulateListingGrid(lstGrid, lstView, txtBox, data.Item2[selNdx], reportTitle, null);
        }

        private void RootListingListViewClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.Assert(sender is SWC.MenuItem);
                SWC.MenuItem menuItem = sender as SWC.MenuItem;
                var lstView = menuItem.Tag as SWC.ListView;
                Debug.Assert(lstView != null);
            }
            catch(Exception ex)
            {
                // TODO JRD
            }
        }

        private static TextBlock GetRoootKindText(GCRootKind kind, int count)
        {
            var txtBlk = new TextBlock();

            switch (kind)
            {
                case GCRootKind.StaticVar:
                    txtBlk.Inlines.Add(new Bold(new Run("Static\nVariables")));
                    break;
                case GCRootKind.ThreadStaticVar:
                    txtBlk.Inlines.Add(new Bold(new Run("Thread\nStatic\nVariables")));
                    break;
                case GCRootKind.LocalVar:
                    txtBlk.Inlines.Add(new Bold(new Run("Local\nVariables")));
                    break;
                case GCRootKind.Strong:
                    txtBlk.Inlines.Add(new Bold(new Run("Strong\nHandles")));
                    break;
                case GCRootKind.Weak:
                    txtBlk.Inlines.Add(new Bold(new Run("Weak\nHandles")));
                    break;
                case GCRootKind.Pinning:
                    txtBlk.Inlines.Add(new Bold(new Run("Strong\nPinning")));
                    break;
                case GCRootKind.Finalizer:
                    txtBlk.Inlines.Add(new Bold(new Run("Finalizer\nQueue")));
                    break;
                case GCRootKind.AsyncPinning:
                    txtBlk.Inlines.Add(new Bold(new Run("Asynch\nPinning")));
                    break;
            }

            txtBlk.Inlines.Add("\n[" + Utils.CountString(count) + "]\n");

            return txtBlk;
        }

        #endregion roots

        #region Finalizer Queue

        private void DisplayFinalizerQueue(DisplayableFinalizerQueue finlQue)
        {
            var grid = this.TryFindResource(GridFinalizerQueue) as Grid;
            grid.Name = GridFinalizerQueue + "__" + Utils.GetNewID();
            Debug.Assert(grid != null);
            grid.Tag = finlQue;
            var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, FinalizerQueueListView);
            Debug.Assert(listView != null);
            listView.Tag = new bool[] { false, false, true }; // column current sorting
            listView.Items.Clear();
            listView.ItemsSource = finlQue.Items;

            var txtBox = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, FinalizerQueTextBox);
            Debug.Assert(txtBox != null);
            txtBox.Text = finlQue.Information;

            var lstBoxObjs = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "FinalizerQueAddresses");
            Debug.Assert(lstBoxObjs != null);
            lstBoxObjs.ContextMenu.Tag = lstBoxObjs;

            //var tab = new CloseableTabItem() { Header = Constants.BlackDiamondPadded + "Finalization", Content = grid, Name = grid.Name + "_tab" };
            //MainTab.Items.Add(tab);
            //MainTab.SelectedItem = tab;
            //MainTab.UpdateLayout();

            DisplayTab(Constants.BlackDiamondPadded, "Finalization", grid, GridFinalizerQueue + "TAB");

            listView.SelectedItem = 0;
        }

        private void FinalizerQueueSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            Debug.Assert(listView != null);
            if (listView.SelectedItem != null)
            {
                var listItem = listView.SelectedItem as FinalizerQueueDisplayableItem;
                Debug.Assert(listItem != null);
                var grid = GetCurrentTabGrid();
                Debug.Assert(grid != null);
                var listBox = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "FinalizerQueAddresses");
                Debug.Assert(listBox != null);
                listBox.ItemsSource = listItem.Addresses;
                listBox.SelectedIndex = 0;
                var label = (Label)LogicalTreeHelper.FindLogicalNode(grid, "FinalizerQueAddressesCounts");
                Debug.Assert(label != null);
                label.Content = listItem.TotalCount + ", unrooted: " + listItem.NotRootedCount;
            }
        }

        private void FinalizerQueueViewHeaderClick(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader column = e.OriginalSource as GridViewColumnHeader;
            if (column == null) return;
            if (column.Role == GridViewColumnHeaderRole.Padding) return;
            string colName = column.Column.Header.ToString();
            var listView = sender as ListView;
            if (listView == null) return;
            var aryToSort = listView.ItemsSource as FinalizerQueueDisplayableItem[];
            Debug.Assert(aryToSort != null);
            if (aryToSort.Length < 2) return;
            int colNdx = Utils.SameStrings(colName, "Total Count")
                ? 0
                : (Utils.SameStrings(colName, "Unrooted Count") ? 1 : 2);
            var sorts = listView.Tag as bool[];
            sorts[colNdx] = !sorts[colNdx];
            Array.Sort(aryToSort, new FinalizerQueueDisplayableItemCmp(colNdx, sorts[colNdx]));
            listView.ItemsSource = aryToSort;
            ICollectionView dataView = CollectionViewSource.GetDefaultView(listView.ItemsSource);
            dataView.Refresh();
            return;
        }

        #endregion Finalizer Queue

        //private void DisplayListViewBottomGrid(ListingInfo info, char prefix, string name, string reportTitle, SWC.MenuItem[] menuItems = null, string filePath = null)
        //{
        //    var grid = this.TryFindResource("ListViewBottomGrid") as Grid;
        //    grid.Name = name + "__" + Utils.GetNewID();
        //    Debug.Assert(grid != null);
        //    string path;
        //    if (filePath == null)
        //        path = CurrentIndex != null ? CurrentIndex.DumpPath : CurrentAdhocDump?.DumpPath;
        //    else
        //        path = filePath;
        //    grid.Tag = new Tuple<string, DumpFileMoniker>(reportTitle, new DumpFileMoniker(path));
        //    var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "TopListView");
        //    GridView gridView = (GridView)listView.View;

        //    // save data and listing name in listView
        //    //
        //    listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);

        //    for (int i = 0, icnt = info.ColInfos.Length; i < icnt; ++i)
        //    {
        //        var gridColumn = new GridViewColumn
        //        {
        //            Header = info.ColInfos[i].Name,
        //            DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
        //            Width = info.ColInfos[i].Width,
        //        };
        //        gridView.Columns.Add(gridColumn);
        //    }

        //    listView.Items.Clear();
        //    listView.ItemsSource = info.Items;
        //    var bottomGrid = (Panel)LogicalTreeHelper.FindLogicalNode(grid, "BottomGrid");
        //    Debug.Assert(bottomGrid != null);
        //    TextBox txtBox = new TextBox
        //    {
        //        HorizontalAlignment = HorizontalAlignment.Stretch,
        //        VerticalAlignment = SW.VerticalAlignment.Stretch,
        //        Foreground = Brushes.DarkGreen,
        //        Text = info.Notes,
        //        FontWeight = FontWeights.Bold
        //    };
        //    bottomGrid.Children.Add(txtBox);

        //    if (menuItems == null)
        //    {
        //        SWC.MenuItem mi = new SWC.MenuItem { Header = "Copy List Row" };
        //        menuItems = new SWC.MenuItem[]
        //        {
        //            mi
        //        };
        //    }
        //    foreach (var menu in menuItems)
        //    {
        //        menu.Tag = listView;
        //        menu.Click += ListViewBottomGridClick;
        //    }
        //    listView.ContextMenu = new SWC.ContextMenu();
        //    listView.ContextMenu.ItemsSource = menuItems;
        //    DisplayTab(prefix, reportTitle, grid, name);
        //}


        private void PopulateListingGrid(Grid grid, ListView listView, TextBox txtBox, ListingInfo info, string reportTitle, string filePath)
        {
            listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);
            string path;
            if (filePath == null)
                path = CurrentIndex != null ? CurrentIndex.DumpPath : CurrentAdhocDump?.DumpPath;
            else
                path = filePath;
            grid.Tag = new Tuple<string, DumpFileMoniker>(reportTitle, new DumpFileMoniker(path));
            GridView gridView = (GridView)listView.View;

            // save data and listing name in listView
            //
            listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);
 //           listView.Items.Clear();
            listView.ItemsSource = info.Items;
            txtBox.Text = info.Notes;
        }

        private ValueTuple<Grid,ListView> GetListingGrid(string name, SWC.MenuItem[] menuItems, ColumnInfo[] colInfos, RoutedEventHandler contextMenuCallback)        {
            var grid = TryFindResource(ListingGrid) as Grid;
            grid.Name = name + "__" + Utils.GetNewID();
            Debug.Assert(grid != null);
            var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, ListingGridView);
            GridView gridView = (GridView)listView.View;

            for (int i = 0, icnt = colInfos.Length; i < icnt; ++i)
            {
                var gridColumn = new GridViewColumn
                {
                    Header = colInfos[i].Name,
                    DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
                    Width = colInfos[i].Width,
                };
                gridView.Columns.Add(gridColumn);
            }

            if (menuItems == null)
            {
                SWC.MenuItem mi = new SWC.MenuItem { Header = "Copy List Row", Tag = listView };
                menuItems = new SWC.MenuItem[]
                {
                    mi
                };
            }
            foreach (var menu in menuItems)
            {
                menu.Tag = listView;
                menu.Click += contextMenuCallback;
            }

            listView.ContextMenu = new SWC.ContextMenu();
            listView.ContextMenu.ItemsSource = menuItems;
            return (grid, listView);
        }


        /// <summary>
        /// Display listing of things, a poor man data grid.
        /// </summary>
        /// <param name="info">Data to be displayed.</param>
        /// <param name="prefix">Symbol (special char) to be put on the tab label.</param>
        /// <param name="name">The grid name, ListingGrid in this case.</param>
        /// <param name="reportTitle">Title to display on the tab label.</param>
        /// <param name="menuItems">Context menu to be attached to ListView control.</param>
        /// <param name="filePath">Some file path, if relevant to listed data.</param>
        private void DisplayListingGrid(ListingInfo info, 
                                        string prefix,
                                        string name,
                                        string reportTitle,
                                        SWC.MenuItem[] menuItems = null,
                                        string filePath = null)
        {
            var grid = TryFindResource(ListingGrid) as Grid;
            Debug.Assert(grid != null);
            grid.Name = name + "__" + Utils.GetNewID();
            string path;
            if (filePath == null)
                path = CurrentIndex != null ? CurrentIndex.DumpPath : CurrentAdhocDump?.DumpPath;
            else
                path = filePath;
            grid.Tag = new Tuple<string, DumpFileMoniker>(reportTitle, new DumpFileMoniker(path));
            var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, ListingGridView);
            GridView gridView = (GridView)listView.View;

            // save data and listing name in listView
            //
            listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);

            for (int i = 0, icnt = info.ColInfos.Length; i < icnt; ++i)
            {
                var gridColumn = new GridViewColumn
                {
                    Header = info.ColInfos[i].Name,
                    DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
                    Width = info.ColInfos[i].Width,
                };
                gridView.Columns.Add(gridColumn);
            }

            listView.Items.Clear();
            listView.ItemsSource = info.Items;

            if (menuItems == null)
            {
                SWC.MenuItem mi = new SWC.MenuItem { Header = "Copy List Row", Tag = listView };
                menuItems = new SWC.MenuItem[]
                {
                    mi
                };
            }
            foreach (var menu in menuItems)
            {
                menu.Tag = listView;
                menu.Click += ListViewBottomGridClick;
            }

            listView.ContextMenu = new SWC.ContextMenu();
            listView.ContextMenu.ItemsSource = menuItems;
            if (!string.IsNullOrEmpty(info.Notes))
            {
                var txtBox = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, ListingGridInformation);
                Debug.Assert(txtBox != null);
                txtBox.Text = info.Notes;
            }

            DisplayTab(prefix, reportTitle, grid, name);
        }

        #region weakreference

        private void DisplayWeakReferenceGrid(ListingInfo info, char prefix, string name, string reportTitle, SWC.MenuItem[] menuItems = null, string filePath = null)
        {
            var mainGrid = this.TryFindResource("WeakReferenceViewGrid") as Grid;
            Debug.Assert(mainGrid != null);
            mainGrid.Name = name + "__" + Utils.GetNewID();
            string path;
            if (filePath == null)
                path = CurrentIndex != null ? CurrentIndex.DumpPath : CurrentAdhocDump?.DumpPath;
            else
                path = filePath;

            var grid = (Grid)LogicalTreeHelper.FindLogicalNode(mainGrid, "WeakReferenceGrid");

            grid.Tag = new Tuple<string, DumpFileMoniker>(reportTitle, new DumpFileMoniker(path));
            var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "WeakReferenceView");
            GridView gridView = (GridView)listView.View;

            // save data and listing name in listView
            //
            listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);

            for (int i = 0, icnt = info.ColInfos.Length; i < icnt; ++i)
            {
                var gridColumn = new GridViewColumn
                {
                    Header = info.ColInfos[i].Name,
                    DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
                    Width = info.ColInfos[i].Width,
                };
                gridView.Columns.Add(gridColumn);
            }

            listView.Items.Clear();
            listView.ItemsSource = info.Items;
            var txtBox = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, "WeakReferenceInformation");
            Debug.Assert(txtBox != null);
            txtBox.Text = info.Notes;
            if (menuItems == null)
            {
                SWC.MenuItem mi = new SWC.MenuItem { Header = "Copy List Row", Tag = listView };
                menuItems = new SWC.MenuItem[]
                {
                    mi
                };
            }
            foreach (var menu in menuItems)
            {
                menu.Tag = listView;
                menu.Click += ListViewBottomGridClick;
            }
            listView.ContextMenu = new SWC.ContextMenu();
            listView.ContextMenu.ItemsSource = menuItems;
            listView.ContextMenu.Tag = listView;

            var lstBoxObjs = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "WeakReferenceObjectAddresses");
            Debug.Assert(lstBoxObjs != null);
            lstBoxObjs.ContextMenu.Tag = lstBoxObjs;
            var lstBoxRoots = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "WeakReferenceRootAddresses");
            Debug.Assert(lstBoxRoots != null);
            lstBoxRoots.ContextMenu.Tag = lstBoxRoots;


            DisplayTab(prefix, reportTitle, mainGrid, name);
            listView.SelectedIndex = 0;
        }

        private void WeakReferenceViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            Debug.Assert(listView != null);
            if (listView.SelectedItem != null)
            {
                var listItem = (listing<string>)listView.SelectedItem;
                int ndx = listItem.Offset / listItem.Count;
                var dataInfo = listView.Tag as Tuple<ListingInfo, string>;
                Debug.Assert(dataInfo != null);
                var data = dataInfo.Item1.Data as KeyValuePair<ulong, ulong[]>[][];
                Debug.Assert(data != null);
                var grid = GetCurrentTabGrid();
                Debug.Assert(grid != null);
                var listBox = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "WeakReferenceObjectAddresses");
                Debug.Assert(listBox != null);
                listBox.Tag = new Tuple<KeyValuePair<ulong, ulong[]>[][], int>(data, ndx);
                listBox.ItemsSource = data[ndx].Select((a) => a.Key);
                listBox.SelectedIndex = 0;
            }
        }

        private void WeakReferenceObjectAddressesChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lb = sender as ListBox;
            Debug.Assert(lb != null);
            if (lb.SelectedIndex >= 0)
            {
                var selInfo = lb.Tag as Tuple<KeyValuePair<ulong, ulong[]>[][], int>;
                Debug.Assert(selInfo != null);
                var grid = GetCurrentTabGrid();
                Debug.Assert(grid != null);
                var listBox = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "WeakReferenceRootAddresses");
                Debug.Assert(listBox != null);
                listBox.ItemsSource = selInfo.Item1[selInfo.Item2][lb.SelectedIndex].Value;
            }

        }

        #endregion weakreference

        #region type values report

        private void DoDisplayTypeValueReportSetup(ClrtDisplayableType dispType)
        {
            var dlg = new TypeValuesReportSetup(CurrentIndex.IndexProxy, dispType) { Owner = this };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteTypeValueQuery(dlg.Query, dlg.Instances));
            }
        }

        private async void ExecuteTypeValueQuery(TypeValueQuery query, ulong[] instances)
        {
            Debug.Assert(instances != null && instances.Length > 0);
            SetStartTaskMainWindowState("Please wait... Type values report: " + query.TypeName);

            var (error, listing) = await Task.Factory.StartNew(() =>
            {
                string err;
                var info = CurrentIndex.GetTypeValuesReport(query, instances, out err);
                return (err, info);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(error == null
                ? "DONE. Type values report: " + query.TypeName
                : "FAILED! Type values report: " + query.TypeName);

            if (error != null)
            {
                ShowError(error);
                return;
            }

            DisplayTypeValuesGrid(listing, Constants.BlackDiamondHeader, TypeValuesReportGrid, Utils.BaseTypeName(query.TypeName));
        }

        private async void ExecuteTypeFieldUsageQuery(string typeName, string fieldTypeName = null, string fieldName = null)
        {
            string baseTypeName = Utils.BaseTypeName(typeName);
            string error = null;
            (string tpName, int typeId, ClrElementKind typeKind, string[] fldTypeNames, int[] fldIds, ClrElementKind[] fldKinds, string[] fldNames)
                = CurrentIndex.GetTypeInfo(typeName, out error);
            if (error != null)
            {
                GuiUtils.ShowError(error,this);
                return;
            }
            if (fieldTypeName == null && fieldName == null) // let user select a field
            {
                Debug.Assert(Utils.SameStrings(typeName, tpName));
                KeyValuePair<string, string>[] fldNameTypes = new KeyValuePair<string, string>[fldIds.Length];
                for (int i = 0, icnt = fldIds.Length; i < icnt; ++i)
                {
                    fldNameTypes[i] = new KeyValuePair<string, string>(fldNames[i], fldTypeNames[i]);
                }
                var dlg = new TypeFieldValueQrySetup(tpName, fldNameTypes) { Owner = this };
                dlg.ShowDialog();
            }
            else if (fieldName == null) // get usage for all fields of FieldTypeName type
            {

            }
            else // get usage for a selected field
            {

            }
            SetStartTaskMainWindowState("Please wait... Type field usage: " + baseTypeName);

            //var (error, listing) = await Task.Factory.StartNew(() =>
            //{
            //    string err;
            //    var info = CurrentIndex.GetTypeValuesReport(query, instances, out err);
            //    return (err, info);
            //}, DumpSTAScheduler);


            SetEndTaskMainWindowState(error == null
                ? "DONE. Type field usage: " + baseTypeName
                : "FAILED! Type field usage: " + baseTypeName);

            if (error != null)
            {
                ShowError(error);
                return;
            }

            // TODO JRD
            //DisplayTypeValuesGrid(listing, Constants.BlackDiamondHeader, TypeValuesReportGrid, Utils.BaseTypeName(query.TypeName));
        }

        private void DisplayTypeValuesGrid(ListingInfo info, string prefix, string name, string reportTitle)
        {
            var grid = TryFindResource(TypeValuesReportGrid) as Grid;
            grid.Name = name + "__" + Utils.GetNewID();
            Debug.Assert(grid != null);
            string path = CurrentIndex != null ? CurrentIndex.DumpPath : CurrentAdhocDump?.DumpPath;
            grid.Tag = new Tuple<string, DumpFileMoniker>(reportTitle, new DumpFileMoniker(path));
            var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, TypeValuesReportView);
            GridView gridView = (GridView)listView.View;

            // save data and listing name in listView
            //
            listView.Tag = new Tuple<ListingInfo, string>(info, reportTitle);

            for (int i = 0, icnt = info.ColInfos.Length; i < icnt; ++i)
            {
                var gridColumn = new GridViewColumn
                {
                    Header = info.ColInfos[i].Name,
                    DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
                    Width = info.ColInfos[i].Width,
                };
                gridView.Columns.Add(gridColumn);
            }

            listView.Items.Clear();
            listView.ItemsSource = info.Items;

            if (!string.IsNullOrEmpty(info.Notes))
            {
                var txtBox = (TextBox)LogicalTreeHelper.FindLogicalNode(grid, TypeValuesReportInfo);
                Debug.Assert(txtBox != null);
                txtBox.Text = info.Notes;
            }

            DisplayTab(prefix, reportTitle, grid, name);
        }

        private void DisplayDependencyNodeGrid(DependencyNode root)
        {
            TreeViewItem tvRoot = new TreeViewItem();
            tvRoot.Header = root.ToString();
            tvRoot.Tag = root;
            Queue<KeyValuePair<DependencyNode, TreeViewItem>> que = new Queue<KeyValuePair<DependencyNode, TreeViewItem>>();
            que.Enqueue(new KeyValuePair<DependencyNode, TreeViewItem>(root, tvRoot));
            while (que.Count > 0)
            {
                var info = que.Dequeue();
                DependencyNode parentNode = info.Key;
                TreeViewItem tvParentNode = info.Value;
                DependencyNode[] descendants = parentNode.Descendants;
                for (int i = 0, icount = descendants.Length; i < icount; ++i)
                {
                    var descNode = descendants[i];
                    TreeViewItem tvNode = new TreeViewItem();
                    tvNode.Header = descNode.ToString();
                    tvNode.Tag = descNode;
                    tvParentNode.Items.Add(tvNode);
                    que.Enqueue(new KeyValuePair<DependencyNode, TreeViewItem>(descNode, tvNode));
                }
            }

            var grid = this.TryFindResource("TreeViewGrid") as Grid;
            Debug.Assert(grid != null);
            grid.Name = "DependencyNodeTreeView__" + Utils.GetNewID();
            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, "treeView");
            Debug.Assert(treeView != null);
            treeView.Tag = root;
            Debug.Assert(treeView != null);
            treeView.Items.Add(tvRoot);

            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Type References", Content = grid, Name = "HeapIndexTypeViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
        }

        private void DisplayTypeAncestorsGrid(AncestorNode root)
        {
            // populate tree view
            TreeViewItem tvRoot = new TreeViewItem();
            tvRoot.Header = root.ToString();
            tvRoot.Tag = root;
            var que = new Queue<KeyValuePair<AncestorNode, TreeViewItem>>();
            que.Enqueue(new KeyValuePair<AncestorNode, TreeViewItem>(root, tvRoot));
            while (que.Count > 0)
            {
                var info = que.Dequeue();
                AncestorNode parentNode = info.Key;
                TreeViewItem tvParentNode = info.Value;
                AncestorNode[] descendants = parentNode.Ancestors;
                for (int i = 0, icount = descendants.Length; i < icount; ++i)
                {
                    var descNode = descendants[i];
                    TreeViewItem tvNode = new TreeViewItem();
                    tvNode.Header = descNode.ToString();
                    tvNode.Tag = descNode;
                    tvParentNode.Items.Add(tvNode);
                    que.Enqueue(new KeyValuePair<AncestorNode, TreeViewItem>(descNode, tvNode));
                }
            }

            // setup grid
            var grid = this.TryFindResource(AncestorTreeViewGrid) as Grid;
            Debug.Assert(grid != null);
            grid.Name = AncestorTreeViewGrid + "__" + Utils.GetNewID();
            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, "AncestorTreeView");
            Debug.Assert(treeView != null);
            treeView.Tag = root;
            Debug.Assert(treeView != null);
            treeView.Items.Add(tvRoot);
            tvRoot.IsExpanded = true;

            // display general information, this will be updated when tree selection changes
            var txtBlk = (TextBlock)LogicalTreeHelper.FindLogicalNode(grid, "AncestorInformation");
            Debug.Assert(txtBlk != null);
            if (root.Data is Tuple<string, int>)
            {
                txtBlk.Inlines.Add(new Run(root.TypeName + " \"") { FontSize = 16, FontWeight = FontWeights.Bold });
                var data = root.Data as Tuple<string, int>;
                var str = data.Item1;
                var cnt = data.Item2; // TODO JRD
                txtBlk.Inlines.Add(new Run(ShortenString(str, 60)) { FontSize = 12, Foreground = Brushes.Green });
                txtBlk.Inlines.Add(new Run("\"") { FontSize = 16, FontWeight = FontWeights.Bold });
            }
            else
            {
                txtBlk.Inlines.Add(new Run(root.TypeName) { FontSize = 16, FontWeight = FontWeights.Bold });
            }

            txtBlk.Inlines.Add(Environment.NewLine);

            txtBlk.Inlines.Add(new Run("") { Name = "Child", Foreground = Brushes.DarkBlue, FontSize = 12, FontStyle = FontStyles.Italic, FontWeight = FontWeights.DemiBold });
            txtBlk.Inlines.Add(Environment.NewLine);
            txtBlk.Inlines.Add(new Run("") { Name = "Parent", Foreground = Brushes.DarkBlue, FontSize = 12, FontStyle = FontStyles.Italic, FontWeight = FontWeights.DemiBold });
            txtBlk.Inlines.Add(Environment.NewLine);
            txtBlk.Inlines.Add(new Run("Instance addresses of the selected node are shown in the list box. To inspect individual instances right click on selected address.") { Foreground = Brushes.DarkBlue, FontSize = 12, FontStyle = FontStyles.Italic, FontWeight = FontWeights.DemiBold });
            txtBlk.Inlines.Add(Environment.NewLine);

            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Type References", Content = grid, Name = "HeapIndexTypeViewTab" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            tvRoot.IsSelected = true;
        }

        private void AncestorTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem item = (TreeViewItem)e.NewValue;
            Debug.Assert(item != null, "AncestorTreeSelectionChanged: selected item is null!");
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (item == null) return;
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
            AncestorNode node = (AncestorNode)item.Tag;
            var addresses = CurrentIndex.GetInstancesAddresses(node.Instances);
            Debug.Assert(CurrentIndex != null);
            var grid = GetCurrentTabGrid();
            var lbAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"AncestorAddressList");
            Debug.Assert(lbAddresses != null);
            lbAddresses.ItemsSource = addresses;
            var txtBlk = (TextBlock)LogicalTreeHelper.FindLogicalNode(grid, "AncestorInformation");

            // first change information of the selected node
            UpdateAncestorInfoLine(txtBlk.Inlines, "Child", node.TypeName);
            if (node.Parent != null)
            {
                var txt = Utils.CountString(node.Instances.Length) + " instance(s) of '" + node.TypeName + "' are referencing";
                UpdateAncestorInfoLine(txtBlk.Inlines, "Child", txt);
                txt = Utils.CountString(node.ReferenceCount) + " unique instance(s) of '" + node.Parent.TypeName + "'";
                UpdateAncestorInfoLine(txtBlk.Inlines, "Parent", txt);
            }
            else
            {
                var txt = Utils.CountString(node.Instances.Length) + " instance(s) of '" + node.TypeName + "' are referenced by";
                UpdateAncestorInfoLine(txtBlk.Inlines, "Child", txt);
                txt = Utils.CountString(node.Ancestors.Length) + " types, total referencing instances " + Utils.CountString(node.AncestorInstanceCount());
                UpdateAncestorInfoLine(txtBlk.Inlines, "Parent", txt);
            }
        }

        private void AncestorExpandAllClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            Debug.Assert(btn != null);
            var grid = GetCurrentTabGrid();
            Debug.Assert(grid != null);
            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, "AncestorTreeView");
            Debug.Assert(treeView != null);
            if (treeView.Items != null && treeView.Items.Count > 0)
            {
                if (btn.Name == "AncestorExpandAll")
                    (treeView.Items[0] as TreeViewItem).ExpandSubtree();
                else if (btn.Name == "AncestorCollapseAll")
                {
                    var root = treeView.Items[0] as TreeViewItem;
                    Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
                    for (int i = 0; i < root.Items.Count; ++i)
                    {
                        stack.Push(root.Items[i] as TreeViewItem);
                    }
                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();
                        current.IsExpanded = false;
                        for (int i = 0; i < current.Items.Count; ++i)
                        {
                            stack.Push(current.Items[i] as TreeViewItem);
                        }
                    }
                }

            }
        }

        private AncestorNode GetAncestorTreeViewSelectedNode()
        {
            var grid = GetCurrentTabGrid();
            Debug.Assert(grid != null);
            if (grid == null) return null;
            var treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(grid, "AncestorTreeView");
            var selected = treeView.SelectedItem as TreeViewItem;
            if (selected == null) return null;
            return selected.Tag as AncestorNode;
        }

        private void TypeTreeCopyTypeNameClicked(object sender, RoutedEventArgs e)
        {
            AncestorNode node = GetAncestorTreeViewSelectedNode();
            if (node != null)
            {
                GuiUtils.CopyToClipboard(node.TypeName);
                MainStatusShowMessage("Copied to Clipboard: " + node.TypeName);
            }
            else
            {
                MainStatusShowMessage("Node not found. Is a tree item selected?");
            }
        }


        private async void TypeTreeGetReferences(object sender, RoutedEventArgs e)
        {
            AncestorNode node = GetAncestorTreeViewSelectedNode();
            if (node == null)
            {
                MainStatusShowMessage("Node not found. Is a tree item selected?");
                return;
            }

            var baseTypeName = Utils.BaseTypeName(node.TypeName);

            ReferenceSearchSetup dlg = new ReferenceSearchSetup(baseTypeName) { Owner = this };
            dlg.ShowDialog();
            if (dlg.Cancelled) return;
            int level = dlg.GetAllReferences ? Int32.MaxValue : dlg.SearchDepthLevel;
            var dispMode = dlg.DisplayMode;
            var searchFlag = dlg.DataSource | dlg.Direction | dlg.Strict;

            SetStartTaskMainWindowState("Getting parent references for: '" + baseTypeName + "', please wait...");

            //var report = await Task.Run(() => CurrentIndex.GetParentTree(typeId, level));
            var report = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetParentTree(node.TypeId, node.Instances, level);
            }, DumpSTAScheduler);

            if (report.Item1 != null)
            {
                MessageBox.Show(report.Item1, "Action Aborted", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                SetEndTaskMainWindowState("Getting parent references for: '" + Utils.BaseTypeName(baseTypeName) + "', failed.");
                return;
            }
            SetEndTaskMainWindowState("Getting parent references for: '" + Utils.BaseTypeName(baseTypeName) + "', done.");
            DisplayTypeAncestorsGrid(report.Item2);
        }

        private void TypeTreeGenerationDistributionClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Not implemented yet.", "TypeTreeGenerationDistributionClicked", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TypeTreeGetTypeSizesClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Not implemented yet.", "TypeTreeGetTypeSizesClicked", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TypeTreeGetTypeStringUsageClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Not implemented yet.", "TypeTreeGetTypeStringUsageClicked", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateAncestorInfoLine(InlineCollection lines, string inlineName, string text)
        {
            Inline line = null;
            foreach (var inline in lines)
            {
                if (Utils.SameStrings(inline.Name, inlineName))
                {
                    line = inline;
                    break;
                }
            }
            if (line != null)
            {
                var newChildLine = new Run(text) { Name = inlineName, FontSize = 12 };
                lines.InsertAfter(line, newChildLine);
                lines.Remove(line);
            }
        }

        private void ListingViewDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            var lv = sender as ListView;
            if (lv == null) return;
            var lvTag = lv.Tag as Tuple<ListingInfo, string>;
            if (lvTag == null) return;
            var tb = e.OriginalSource as TextBlock;
            if (tb == null) return;
            string item = tb.Text;
            var tbParent = tb.Parent as System.Windows.Controls.GridViewRowPresenter;
            if (tbParent == null)
                throw new MdrDeskException("[MainWindow.ListingViewDoubleClicked] TextBlock parent is not GridViewRowPresenter as expected.");
            var lstInfo = lvTag.Item1;
            var listing = (listing<string>)tbParent.Content;
            int ndx = Constants.InvalidIndex;
            for (int i = 0, icnt = listing.Count; i < icnt; ++i)
            {
                string lstVal = listing.GetItem(i);
                if (Utils.SameStrings(item,lstVal))
                {
                    ndx = i;
                    break;
                }
            }
            if (ndx == Constants.InvalidIndex) return;
            ulong addr = GetAddressFromEntry(item);
            if (addr == Constants.InvalidAddress) return;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() => ExecuteInstanceValueQuery("Getting object value at: " + Utils.RealAddressString(addr), addr));
        }

        #endregion type values report

        #region instance value

        /// <summary>
        /// List of currently displayed windows.
        /// Used to close them when index is closing.
        /// </summary>
        private ConcurrentDictionary<int, Window> _wndDct = new ConcurrentDictionary<int, Window>();

        public async void ExecuteInstanceValueQuery(string msg, ulong addr)
        {
            SetStartTaskMainWindowState(msg);

            (string error, InstanceValue inst, TypeExtractor.KnownTypes knownType) = await Task.Factory.StartNew(() =>
            {
                return CurrentIndex.GetInstanceValue(addr, null);
            }, DumpSTAScheduler);

            SetEndTaskMainWindowState(error == null
                ? "value at " + Utils.RealAddressString(addr) + ":  " + inst.Value.ToString()
                : msg + " failed.");

            if (error != null)
            {
                ShowError(error);
                return;
            }

            Debug.Assert(inst != null);

            if (knownType != TypeExtractor.KnownTypes.Unknown)
            {
                if (knownType == TypeExtractor.KnownTypes.StringBuilder)
                {
                    ValueWindows.ShowContentWindow(inst.GetDescription(), inst, ValueWindows.WndType.Content);
                }
                if ((inst.KeyValuePairs == null || inst.KeyValuePairs.Length < 1) && (inst.ArrayValues==null || inst.ArrayValues.Length < 1))
                {
                    ShowInformation("Empty Collection", TypeExtractor.GetKnowTypeName(knownType), "The collection at address " + Utils.RealAddressString(addr) + " is empty.", inst.TypeName);
                    return;
                }

                if (knownType == TypeExtractor.KnownTypes.HashSet || knownType == TypeExtractor.KnownTypes.List)
                {
                    ValueWindows.ShowContentWindow(inst.GetDescription(), inst, ValueWindows.WndType.List);
                    return;
                }
                {
                    ValueWindows.ShowContentWindow(inst.GetDescription(), inst, ValueWindows.WndType.KeyValues);
                    return;
                }
            }

            if (inst.IsArray())
            {
                ValueWindows.ShowContentWindow(inst.GetDescription(), inst, ValueWindows.WndType.List);
            }
            else if (!inst.HaveFields())
            {
                ValueWindows.ShowContentWindow(inst.GetDescription(), inst, ValueWindows.WndType.Content);
            }
            else
            {
                ValueWindows.ShowContentWindow(inst.GetDescription(), inst, ValueWindows.WndType.Tree);
            }
        }
         
        #endregion instance value

        #region threads

        private async void ExecuteGetThreadinfos()
        {
            SetStartTaskMainWindowState("Getting thread infos, please wait...");

            //var result = await Task.Run(() =>
            //{
            //    string error = null;
            //    var info = CurrentIndex.GetThreads(out error);
            //    return new Tuple<string, ClrtThread[], string[], KeyValuePair<int, ulong>[]>(error, info.Item1, info.Item2, info.Item3);
            //});
            var result = await Task.Factory.StartNew(() =>
            {
                string error = null;
                var info = CurrentIndex.GetThreads(out error);
                return new Tuple<string, ClrtThread[], string[], KeyValuePair<int, ulong>[]>(error, info.Item1, info.Item2, info.Item3);
            }, DumpSTAScheduler);

            string msg = result.Item1 != null ? "Getting thread infos failed." : "Getting thread infos succeeded.";
            SetEndTaskMainWindowState(msg);
            if (result.Item1 != null)
            {
                GuiUtils.ShowError(result.Item1, this);
                return;
            }

            var threads = result.Item2;
            var framesMethods = result.Item3;

            int[][] frames = new int[threads.Length][];
            for (int i = 0, icnt = threads.Length; i < icnt; ++i)
                frames[i] = threads[i].Frames;

            var frmCmp = new Utils.IntArrayHeadCmp();
            int[] frMap = Utils.Iota(threads.Length);
            Array.Sort(frames, frMap, frmCmp);
            int cnt, frmId = 0;
            KeyValuePair<int, int>[] frmCounts = new KeyValuePair<int, int>[frames.Length];
            frmCounts[0] = new KeyValuePair<int, int>(frmId, 1);
            for (int i = 1; i < frames.Length; ++i)
            {
                if (frmCmp.Compare(frames[i - 1], frames[i]) == 0)
                {
                    cnt = frmCounts[i - 1].Value;
                    frmCounts[i] = new KeyValuePair<int, int>(frmId, cnt + 1);
                    continue;
                }
                ++frmId;
                frmCounts[i] = new KeyValuePair<int, int>(frmId, 1);
            }

            int digitCount = Utils.NumberOfDigits(frmId);
            char[] buf = new char[digitCount];

            cnt = frmCounts[frmCounts.Length - 1].Value;
            frmId = frmCounts[frmCounts.Length - 1].Key;
            for (int i = frmCounts.Length - 2; i >= 0; --i)
            {
                if (frmCounts[i].Key == frmId)
                {
                    frmCounts[i] = new KeyValuePair<int, int>(frmId, cnt);
                    continue;
                }
                cnt = frmCounts[i].Value;
                frmId = frmCounts[i].Key;
            }

            int[] frMap2 = Utils.Iota(threads.Length);
            Array.Sort(frMap, frMap2);
            frMap = null;

            const int ColumnCount = 5;
            string[] data = new string[threads.Length * ColumnCount];
            listing<string>[] items = new listing<string>[threads.Length];
            int dataNdx = 0;
            int itemNdx = 0;
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);

            for (int i = 0, icnt = threads.Length; i < icnt; ++i)
            {
                var thrd = threads[i];
                var osIdStr = thrd.OSThreadId.ToString();
                var osIdStrx = thrd.OSThreadId.ToString("x");
                var mngIdStr = thrd.ManagedThreadId.ToString();

                sb.Clear();
                var traits = thrd.GetTraitsString(sb);

                items[itemNdx++] = new listing<string>(data, dataNdx, ColumnCount);
                data[dataNdx++] = osIdStr;
                data[dataNdx++] = osIdStrx;
                data[dataNdx++] = mngIdStr;
                data[dataNdx++] = traits;

                KeyValuePair<int, int> kv = frmCounts[frMap2[i]];
                sb.Clear();
                var id = Utils.GetSubscriptIntStr(kv.Key, digitCount); // Utils.GetDigitsString(kv.Key, digitCount, buf); // Utils.GetFancyIntStr(kv.Key,digitCount); // 
                sb.Append(id).Append("/").Append(kv.Value).Append(" thread(s), trace count ").Append(threads[i].Frames.Length);

                data[dataNdx++] = sb.ToString();
            }

            ColumnInfo[] colInfos = new[]
            {
                new ColumnInfo("OS Id", ReportFile.ColumnType.Int32, 100, 1, true),
                new ColumnInfo("OS Id x", ReportFile.ColumnType.String, 100, 2, true),
                new ColumnInfo("Mng Id", ReportFile.ColumnType.Int32, 100, 3, true),
                new ColumnInfo("Properties", ReportFile.ColumnType.String, 300, 4, true),
                new ColumnInfo("Frame Id/Thread and Trace Counts", ReportFile.ColumnType.String, 400, 5, true),
            };

            sb.Clear();
            sb.Append(ReportFile.DescrPrefix).Append("Thread Count ").Append(threads.Length).AppendLine();

            Tuple<ClrtThread[], string[], KeyValuePair<int, ulong>[]> dataInfo = new Tuple<ClrtThread[], string[], KeyValuePair<int, ulong>[]>(threads, framesMethods, result.Item4);

            var listing = new ListingInfo(null, items, colInfos, sb.ToString(), dataInfo);

            var grid = TryFindResource(ThreadViewGrid) as Grid;
            Debug.Assert(grid != null);
            var listView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "ThreadListingView");
            Debug.Assert(listView != null);

            //GuiUtils.AddListViewColumn(grid, "AliveStackObjects", "Alive Stack Objects", 400);
            //GuiUtils.AddListViewColumn(grid, "DeadStackObjects", "Dead Stack Objects", 400);

            //var alistView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "AliveStackObjects");
            //GridView agridView = (GridView)alistView.View;
            //var agridColumn = new GridViewColumn
            //{
            //	Header = "Alive Stack Objects",
            //};
            //agridView.Columns.Add(agridColumn);


            listView.Tag = new Tuple<ListingInfo, string>(listing, "Thread View");
            GridView gridView = (GridView)listView.View;
            for (int i = 0, icnt = listing.ColInfos.Length; i < icnt; ++i)
            {
                var gridColumn = new GridViewColumn
                {
                    Header = listing.ColInfos[i].Name,
                    DisplayMemberBinding = new Binding(listing<string>.PropertyNames[i]),
                    Width = listing.ColInfos[i].Width,
                };
                gridView.Columns.Add(gridColumn);
            }
            listView.Items.Clear();
            listView.ItemsSource = listing.Items;

            var alistView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "AliveStackObjects");
            Debug.Assert(alistView != null);
            alistView.ContextMenu.Tag = alistView;
            alistView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "DeadStackObjects");
            Debug.Assert(alistView != null);
            alistView.ContextMenu.Tag = alistView;

            StringBuilderCache.Release(sb);

            grid.Name = ThreadViewGrid + "__" + Utils.GetNewID();
            DisplayTab(Constants.BlackDiamond, "Threads", grid, ThreadViewGrid);
            listView.SelectedIndex = 0;
        }

        private void ThreadListingView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            Debug.Assert(listView != null);
            if (listView.SelectedItem == null) return;
            listing<string> selected = (listing<string>)listView.SelectedItem;
            // get index of data
            Tuple<ListingInfo, string> info = listView.Tag as Tuple<ListingInfo, string>;
            Debug.Assert(info != null);
            var data = info.Item1.Data as Tuple<ClrtThread[], string[], KeyValuePair<int, ulong>[]>;
            Debug.Assert(data != null);
            int dataNdx = selected.Offset / selected.Count;
            ClrtThread thread = data.Item1[dataNdx];
            var grid = GetCurrentTabGrid();
            Debug.Assert(grid != null);
            var lstBox = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, "ThreadListingFrames");
            Debug.Assert(lstBox != null);
            lstBox.Items.Clear();
            string[] allFrames = data.Item2;
            int frameCount = thread.Frames.Length;
            for (int i = 0; i < frameCount; ++i)
            {
                lstBox.Items.Add(allFrames[thread.Frames[i]]);
            }
            var stackVars = CurrentIndex.GetThreadStackVarsStrings(thread.LiveStackObjects, thread.DeadStackObjects, data.Item3);
            var alistView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "AliveStackObjects");
            Debug.Assert(alistView != null);
            alistView.ItemsSource = stackVars.Key;
            alistView = (ListView)LogicalTreeHelper.FindLogicalNode(grid, "DeadStackObjects");
            Debug.Assert(alistView != null);
            alistView.ItemsSource = stackVars.Value;

        }

        #endregion threads

        #region Instance Hierarchy Traversing

        private async void ExecuteInstanceHierarchyQuery(string statusMessage, ulong addr, int fldNdx)
        {
            SetStartTaskMainWindowState(statusMessage + ", please wait...");
            var result = await Task.Factory.StartNew(() =>
            {
                string error;
                InstanceValueAndAncestors instanceInfo = CurrentIndex.GetInstanceInfo(Utils.RealAddress(addr), fldNdx, out error);
                return Tuple.Create(error, instanceInfo);
            }, DumpSTAScheduler);

            if (result.Item1 != null)
            {
                SetEndTaskMainWindowState(statusMessage + ", FAILED.");
                if (result.Item1[0] == Constants.InformationSymbol)
                    ShowInformation("Instance Hierarchy", "Instance " + Utils.AddressString(addr) + " seatrch failed", result.Item1, null);
                else
                    ShowError(result.Item1);
                return;
            }

            SetEndTaskMainWindowState(statusMessage + ", DONE.");
            DisplayInstanceHierarchyGrid(result.Item2);
        }

        private void DisplayInstanceHierarchyGrid(InstanceValueAndAncestors instanceInfo)
        {
            var mainGrid = this.TryFindResource("InstanceHierarchyGrid") as Grid;
            Debug.Assert(mainGrid != null);
            mainGrid.Name = "InstanceHierarchyGrid__" + Utils.GetNewID();
            var undoRedoList = new UndoRedoList<InstanceValueAndAncestors, Tuple<ulong, int>>(new InstanceHierarchyKeyEqCmp());
            undoRedoList.Add(instanceInfo);
            mainGrid.Tag = undoRedoList;
            TreeViewItem root;
            TreeView treeView;
            var ancestorList = UpdateInstanceHierarchyGrid(instanceInfo, mainGrid, out treeView, out root);

            var tab = new CloseableTabItem() { Header = Constants.BlackDiamond + " Instance Hierarchy", Content = mainGrid, Name = "InstanceHierarchyGrid" };
            MainTab.Items.Add(tab);
            MainTab.SelectedItem = tab;
            MainTab.UpdateLayout();
            ancestorList.SelectedIndex = 0;
        }

        private ListBox UpdateInstanceHierarchyGrid(InstanceValueAndAncestors instanceInfo, Grid mainGrid, out TreeView treeView, out TreeViewItem tvRoot)
        {
            InstanceValue instVal = instanceInfo.Instance;
            var stackPanel = new StackPanel() { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var textBlk = new TextBlock();
            textBlk.Inlines.Add(instVal.ToString());
            stackPanel.Children.Add(textBlk);
            tvRoot = new TreeViewItem
            {
                Header = GuiUtils.GetInstanceValueStackPanel(instVal),
                Tag = instVal
            };

            var que = new Queue<KeyValuePair<InstanceValue, TreeViewItem>>();
            que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(instVal, tvRoot));
            while (que.Count > 0)
            {
                var info = que.Dequeue();
                InstanceValue parentNode = info.Key;
                TreeViewItem tvParentNode = info.Value;
                InstanceValue[] descendants = parentNode.Fields;
                for (int i = 0, icount = descendants.Length; i < icount; ++i)
                {
                    var descNode = descendants[i];
                    var tvNode = new TreeViewItem
                    {
                        Header = GuiUtils.GetInstanceValueStackPanel(descNode),
                        Tag = descNode
                    };
                    tvParentNode.Items.Add(tvNode);
                    que.Enqueue(new KeyValuePair<InstanceValue, TreeViewItem>(descNode, tvNode));
                }
            }

            var mainLabel = (Label)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyValueLabel");
            mainLabel.Content = instVal;

            var ancestorNameList = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyAncestorNames");
            ancestorNameList.ItemsSource = instanceInfo.Ancestors;

            var treeViewGrid = (Grid)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyFieldGrid");
            Debug.Assert(treeViewGrid != null);
            treeView = (TreeView)LogicalTreeHelper.FindLogicalNode(treeViewGrid, "InstHierarchyFieldTreeview");
            Debug.Assert(treeView != null);
            treeView.Items.Clear();
            treeView.Items.Add(tvRoot);
            var lstAddresses = (ListBox)LogicalTreeHelper.FindLogicalNode(mainGrid, "InstHierarchyAncestorAddresses");
            lstAddresses.ItemsSource = null;
            tvRoot.IsSelected = true;
            tvRoot.ExpandSubtree();
            return ancestorNameList;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private void InstHierarchyAncestorChanged(object sender, SelectionChangedEventArgs e)
        {
            var lstBox = sender as ListBox;
            Debug.Assert(lstBox != null);
            var selectedItem = lstBox.SelectedItem;
            if (selectedItem == null) return;
            var grid = GetCurrentTabGrid();
            var lbInstances = (ListBox)LogicalTreeHelper.FindLogicalNode(grid, @"InstHierarchyAncestorAddresses");
            Debug.Assert(lbInstances != null);
            lbInstances.ItemsSource = (selectedItem as AncestorDispRecord).Instances;
        }

        private async void InstHierarchyTreeViewDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            TreeView tv = sender as TreeView;
            var selItem = tv.SelectedItem as TreeViewItem;
            var instValue = selItem.Tag as InstanceValue;
            Debug.Assert(instValue != null);
            if (instValue.Address != Constants.InvalidAddress)
            {
                var mainGrid = GetCurrentTabGrid();
                Debug.Assert(mainGrid != null);
                var undoList = mainGrid.Tag as UndoRedoList<InstanceValueAndAncestors, Tuple<ulong, int>>;
                ulong addr = instValue.Address;
                int fldNdx = instValue.FieldIndex;
                var existing = undoList.GetExisting(new Tuple<ulong, int>(addr, fldNdx));

                Tuple<string, InstanceValueAndAncestors> result;
                if (existing == null)
                {
                    SetStartTaskMainWindowState("Getting instance info" + ", please wait...");
                    result = await Task.Factory.StartNew(() =>
                    {
                        string error;
                        InstanceValueAndAncestors instanceInfo = CurrentIndex.GetInstanceInfo(addr, fldNdx, out error);
                        return Tuple.Create(error, instanceInfo);
                    }, DumpSTAScheduler);

                    if (result.Item1 != null)
                    {
                        SetEndTaskMainWindowState("Getting instance info" + ", FAILED.");
                        if (result.Item1[0] == Constants.InformationSymbol)
                            ShowInformation("Instance Hierarchy", "Instance " + Utils.AddressString(addr) + " seatrch failed", result.Item1, null);
                        else
                            ShowError(result.Item1);
                        return;
                    }
                    undoList.Add(result.Item2);
                }
                else
                {
                    result = new Tuple<string, InstanceValueAndAncestors>(null, existing);
                }

                SetEndTaskMainWindowState("Getting instance info" + ", DONE.");
                TreeViewItem tvRoot;
                TreeView treeView;
                var ancestorList = UpdateInstanceHierarchyGrid(result.Item2, mainGrid, out treeView, out tvRoot);
                ancestorList.SelectedIndex = 0;
            }
        }

        private async void InstHierarchyAncestorAddressesDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            var lstBox = sender as ListBox;
            Debug.Assert(lstBox != null);
            if (lstBox.SelectedItem != null)
            {
                var selectedAddress = (ulong)lstBox.SelectedItem;
                Tuple<string, InstanceValueAndAncestors> result;
                // ancestors cannot be structures so maybe the address is in the undo cache
                var mainGrid = GetCurrentTabGrid();
                Debug.Assert(mainGrid != null);
                var undoList = mainGrid.Tag as UndoRedoList<InstanceValueAndAncestors, Tuple<ulong, int>>;
                var key = new Tuple<ulong, int>(selectedAddress, Constants.InvalidIndex);
                InstanceValueAndAncestors instanceInfo = undoList.GetExisting(key);
                if (instanceInfo == null)
                {
                    SetStartTaskMainWindowState("Getting instance info" + ", please wait...");
                    result = await Task.Factory.StartNew(() =>
                    {
                        string error;
                        instanceInfo = CurrentIndex.GetInstanceInfo(selectedAddress, Constants.InvalidIndex, out error);
                        return Tuple.Create(error, instanceInfo);
                    }, DumpSTAScheduler);

                    if (result.Item1 != null)
                    {
                        SetEndTaskMainWindowState("Getting instance info" + ", FAILED.");
                        if (result.Item1[0] == Constants.InformationSymbol)
                            ShowInformation("Instance Hierarchy", "Instance " + Utils.AddressString(selectedAddress) + " seatrch failed",
                                result.Item1, null);
                        else
                            ShowError(result.Item1);
                        return;
                    }
                    undoList.Add(result.Item2);
                    SetEndTaskMainWindowState("Getting instance info" + ", DONE.");
                }
                else
                {
                    result = new Tuple<string, InstanceValueAndAncestors>(null, instanceInfo);
                }
                TreeViewItem tvItem;
                TreeView treeView;
                var ancestorList = UpdateInstanceHierarchyGrid(result.Item2, mainGrid, out treeView, out tvItem);
                ancestorList.SelectedIndex = 0;
                tvItem.IsSelected = true;
            }
        }

        private void InstHierarchyUndoClicked(object sender, RoutedEventArgs e)
        {
            InstHierarchyRedoUndo(true);
        }

        private void InstHierarchyRedoClicked(object sender, RoutedEventArgs e)
        {
            InstHierarchyRedoUndo(false);
        }

        private void InstHierarchyRedoUndo(bool undo)
        {
            var mainGrid = GetCurrentTabGrid();
            Debug.Assert(mainGrid != null);
            var undoList = mainGrid.Tag as UndoRedoList<InstanceValueAndAncestors, Tuple<ulong, int>>;
            Debug.Assert(undoList != null);
            bool can;
            var data = undo ? undoList.Undo(out can) : undoList.Redo(out can);
            if (can)
            {
                TreeViewItem tvItem;
                TreeView treeView;
                var ancestorList = UpdateInstanceHierarchyGrid(data, mainGrid, out treeView, out tvItem);
                ancestorList.SelectedIndex = 0;
                tvItem.IsSelected = true;
            }
        }

        #endregion Instance Hierarchy Traversing

        #region msagl graph

        private void DisplayGraph(Digraph digraph)
        {
            Grid mainGrid = new Grid();
            DockPanel graphViewerPanel = new DockPanel();
            ToolBar toolBar = new ToolBar();
            GraphViewer graphViewer = new GraphViewer();
        }

        #endregion msagl graph

        #endregion Display Grids

        #region MessageBox

        private void ShowInformation(string caption, string header, string text, string details)
        {
            var dialog = new MdrMessageBox()
            {
                Owner = this,
                Caption = string.IsNullOrWhiteSpace(caption) ? "Message" : caption,
                InstructionHeading = string.IsNullOrWhiteSpace(header) ? "???" : header,
                InstructionText = string.IsNullOrWhiteSpace(text) ? String.Empty : text,
                DeatilsText = string.IsNullOrWhiteSpace(details) ? String.Empty : details
            };
            dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
            dialog.DetailsExpander.Visibility = string.IsNullOrEmpty(details) ? Visibility.Collapsed : Visibility.Visible;
            dialog.ShowDialog();
        }

        private void ShowError(string errStr)
        {
            MdrMessageBox dialog;
            if (string.IsNullOrWhiteSpace(errStr))
            {
                dialog = new MdrMessageBox()
                {
                    Owner = this,
                    Caption = "INVALID ERROR",
                    InstructionHeading = "ShowError called with null/empty error string.",
                    InstructionText = "Please copy the details text below, and send it to jdominos@gmail.com. Thank you.",
                    DeatilsText = Environment.StackTrace
                };
            }
            string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);

            if (parts.Length > 2)
            {
                dialog = new MdrMessageBox()
                {
                    Owner = this,
                    Caption = parts[0],
                    InstructionHeading = parts[1],
                    InstructionText = parts[2],
                    DeatilsText = parts.Length > 3 ? parts[3] : string.Empty
                };
            }
            else if (parts.Length > 1)
            {
                dialog = new MdrMessageBox()
                {
                    Owner = this,
                    Caption = "ERROR",
                    InstructionHeading = parts[0],
                    InstructionText = parts[1],
                    DeatilsText = string.Empty
                };
            }
            else
            {
                dialog = new MdrMessageBox()
                {
                    Owner = this,
                    Caption = "ERROR",
                    InstructionHeading = "ERROR",
                    InstructionText = errStr,
                    DeatilsText = string.Empty
                };
            }

            dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
            dialog.DetailsExpander.Visibility = string.IsNullOrWhiteSpace(dialog.DeatilsText) ? Visibility.Collapsed : Visibility.Visible;
            var result = dialog.ShowDialog();
        }

        private void MessageBoxShowError(string errStr)
        {
            string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);
            Debug.Assert(parts.Length > 2);
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxCapacity);
            for (int i = 1, icnt = parts.Length; i < icnt; ++i)
                sb.AppendLine(parts[i]);
            MessageBox.Show(StringBuilderCache.GetStringAndRelease(sb), parts[0], MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private MdrMessageBox GetErrorMsgBox(string errStr)
        {
            string[] parts = errStr.Split(new[] { Constants.HeavyGreekCrossPadded }, StringSplitOptions.None);
            Debug.Assert(parts.Length > 2);
            var dialog = new MdrMessageBox()
            {
                Owner = this,
                Caption = parts[0],
                InstructionHeading = parts[1],
                InstructionText = parts[2],
                DeatilsText = parts.Length > 3 ? parts[3] : string.Empty
            };
            dialog.SetButtonsPredefined(EnumPredefinedButtons.Ok);
            dialog.DetailsExpander.Visibility = string.IsNullOrWhiteSpace(dialog.DeatilsText) ? Visibility.Collapsed : Visibility.Visible;
            return dialog;
        }

        #endregion MessageBox

        #region Map Queries

        //private async void ExecuteGenerationQuery(string statusMessage, ulong[] addresses, Grid grid)
        //{
        //    SetStartTaskMainWindowState(statusMessage + ", please wait...");

        //    //var result = await Task.Run(() =>
        //    //{
        //    //    return CurrentIndex.GetGenerationHistogram(addresses);
        //    //});
        //    var result = await Task.Factory.StartNew(() =>
        //    {
        //        return CurrentIndex.GetGenerationHistogram(addresses);
        //    }, DumpSTAScheduler);


        //    Expander expander = null;
        //    if (grid.Name.StartsWith("HeapIndexTypeView__"))
        //        expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"TypeViewDataExpander");
        //    else
        //        expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"ExtraDataExpander");

        //    if (expander == null)
        //    {
        //        var genStr = ClrtSegment.GetGenerationHistogramSimpleString(result);
        //        SetEndTaskMainWindowState("Object generation at address(s): " + genStr);
        //        return;
        //    }

        //    System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();

        //    host.Child = DmpNdxQueries.Auxiliaries.getColumnChart(ClrtSegment.GetGenerationHistogramTuples(result));

        //    if (addresses.Length == 1)
        //        expander.Header = "Generation histogram for instance at: " + Utils.AddressString(addresses[0]) + " " + ClrtSegment.GetGenerationHistogramSimpleString(result);
        //    else
        //        expander.Header = "Generation histogram: " + ClrtSegment.GetGenerationHistogramSimpleString(result);
        //    host.HorizontalAlignment = HorizontalAlignment.Stretch;
        //    host.VerticalAlignment = SW.VerticalAlignment.Stretch;
        //    var expanderGrid = new Grid { Height = 100 };
        //    expanderGrid.Children.Add(host);
        //    host.HorizontalAlignment = HorizontalAlignment.Stretch;
        //    expander.Content = expanderGrid;
        //    expander.IsExpanded = true;

        //    SetEndTaskMainWindowState(statusMessage + ", DONE.");
        //}

        //private async void ExecuteGenerationQuery(string statusMessage, string reportTitle, string str, Grid grid)
        //{
        //    MainStatusShowMessage(statusMessage + ", please wait...");
        //    MainToolbarTray.IsEnabled = false;

        //    Mouse.OverrideCursor = Cursors.Wait;
        //    //var result = await Task.Run(() =>
        //    //{
        //    //    string error;
        //    //    int[] genHistogram = null;
        //    //    switch (reportTitle)
        //    //    {
        //    //        case ReportTitleStringUsage:
        //    //            genHistogram = CurrentIndex.GetStringGcGenerationHistogram(str, out error);
        //    //            break;
        //    //        default:
        //    //            genHistogram = CurrentIndex.GetTypeGcGenerationHistogram(str, out error);
        //    //            break;
        //    //    }
        //    //    return new Tuple<string, int[]>(error, genHistogram);
        //    //});
        //    var result = await Task.Factory.StartNew(() =>
        //    {
        //        string error;
        //        int[] genHistogram = null;
        //        switch (reportTitle)
        //        {
        //            case ReportTitleStringUsage:
        //                genHistogram = CurrentIndex.GetStringGcGenerationHistogram(str, out error);
        //                break;
        //            default:
        //                genHistogram = CurrentIndex.GetTypeGcGenerationHistogram(str, out error);
        //                break;
        //        }
        //        return new Tuple<string, int[]>(error, genHistogram);
        //    }, DumpSTAScheduler);

        //    Mouse.OverrideCursor = null;
        //    MainToolbarTray.IsEnabled = true;
        //    if (result.Item1 != null)
        //    {
        //        MainStatusShowMessage(statusMessage + ": FAILED");
        //        MessageBox.Show(result.Item1, "Generation Lookup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return;
        //    }
        //    else
        //        MainStatusShowMessage(statusMessage + ": DONE");

        //    Expander expander = null;
        //    if (grid.Name.StartsWith("HeapIndexTypeView__"))
        //        expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"TypeViewDataExpander");
        //    else
        //        expander = (Expander)LogicalTreeHelper.FindLogicalNode(grid, @"ExtraDataExpander");

        //    System.Windows.Forms.Integration.WindowsFormsHost host = new System.Windows.Forms.Integration.WindowsFormsHost();

        //    host.Child = DmpNdxQueries.Auxiliaries.getColumnChart(ClrtSegment.GetGenerationHistogramTuples(result.Item2));

        //    expander.Header = "Generation histogram for " + str + " " + ClrtSegment.GetGenerationHistogramSimpleString(result.Item2);
        //    host.HorizontalAlignment = HorizontalAlignment.Stretch;
        //    host.VerticalAlignment = SW.VerticalAlignment.Stretch;
        //    var expanderGrid = new Grid { Height = 100 };
        //    expanderGrid.Children.Add(host);
        //    host.HorizontalAlignment = HorizontalAlignment.Stretch;
        //    expander.Content = expanderGrid;
        //    expander.IsExpanded = true;
        //}


        #endregion Map Queries

        #region TabItem Cleanup

        private async void CloseCurrentIndex()
        {
            MainStatusShowMessage("Closing current index...");
            ValueWindows.CloseAll();
            foreach (var kv in _wndDct)
            {
                kv.Value.Close();
            }
            _wndDct.Clear();
            MainTab.Items.Clear();
            if (IsIndexAvailable(null))
            {
                var result = await Task.Factory.StartNew(() =>
                {
                    CurrentIndex.Dispose();
                    CurrentIndex = null;
                    return true;
                }, _dumpSTAScheduler);
            }
            MainStatusShowMessage("Current index is no more.");
        }


        public void ClearTabItem(Grid grid)
        {
            // TODO JRD -- handle all grid types

            grid.Tag = null;
            var gridNamePrefix = Utils.GetNameWithoutId(grid.Name);
            switch (gridNamePrefix)
            {
                case "InstanceHierarchyGrid":
                    break;
            }
        }

        #endregion TabItem Cleanup

        #region Utils

        private static long _lastEnteredValue = 0;

        public Grid FindResourceGrid(string gridName)
        {
            var grid = TryFindResource(gridName) as Grid;
            Debug.Assert(grid != null);
            grid.Name = gridName + "__" + Utils.GetNewID();
            return grid;
        }

        private bool GetUserEnteredNumber(string title, string descr, out long value)
        {
            value = 0;
            string str;
            if (!GetDlgString(title,
                string.IsNullOrWhiteSpace(descr) ? "Enter number:" : descr,
                _lastEnteredValue > 0 ? _lastEnteredValue.ToString() : " ",
                out str)) return false;
            str = str.Trim();
            if (!Int64.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                MessageBox.Show("Not valid number string: " + str + ".", "INVALID INPUT", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            _lastEnteredValue = value;
            return true;
        }

        private bool GetUserEnteredAddress(string title, string descr, out ulong value)
        {
            value = 0;
            string str;
            if (!GetDlgString(title,
                string.IsNullOrWhiteSpace(descr) ? "Enter number:" : descr,
                " ",
                out str)) return false;
            str = str.Trim();
            bool parseResult = false;
            if (str.Length > 0 && (str[0] == 'n' || str[0] == 'N'))
            {
                str = str.Substring(1);
                parseResult = UInt64.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
            }
            else
            {
                if (str.Length > 0 && str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) str = str.Substring(2);
                parseResult = UInt64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }
            if (!parseResult)
            {
                MessageBox.Show("Not valid number string: " + str + ".", "INVALID INPUT", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            return true;
        }


        private string ShortenString(string str, int len)
        {
            if (str.Length > len)
                return str.Substring(0, len - 1) + Constants.HorizontalEllipsisChar;
            return str;
        }


        public ListView GetCurrentListView(string name)
        {
            var grid = GetCurrentTabGrid();
            if (grid == null) return null;
            object lv = LogicalTreeHelper.FindLogicalNode(grid, name);
            if (lv is ListView) return lv as ListView;
            return null;
        }

        public ulong GetAddressFromEntry(string entry)
        {
            ulong addr = GuiUtils.TryGetAddressValue(entry);
            if (addr != Constants.InvalidAddress)
            {
                return IsValidHeapAddress(addr) ? addr : Constants.InvalidAddress;
            }
            return Constants.InvalidAddress;
        }

        //public static void SetSelectedItem(TreeView control, object item)
        //{
        //	try
        //	{
        //		var dObject = control.ItemContainerGenerator.ContainerFromItem(item);

        //		//uncomment the following line if UI updates are unnecessary
        //		((TreeViewItem)dObject).IsSelected = true;

        //		MethodInfo selectMethod = typeof(TreeViewItem).GetMethod("Select",
        //			BindingFlags.NonPublic | BindingFlags.Instance);

        //		selectMethod.Invoke(dObject, new object[] { true });
        //	}
        //	catch { }
        //}

        //private StackPanel GetClrtDisplayableTypeStackPanel(ClrtDisplayableType dispType)
        //{
        //	var stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };
        //	var kind = dispType.Kind;
        //	SWC.Image image = new SWC.Image();

        //	switch (TypeKinds.GetMainTypeKind(kind))
        //	{
        //		case TypeKind.StringKind:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("PrimitivePng")).Source;
        //			break;
        //		case TypeKind.InterfaceKind:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("InterfacePng")).Source;
        //			break;
        //		case TypeKind.ArrayKind:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("ArrayPng")).Source;
        //			break;
        //		case TypeKind.StructKind:
        //			switch (TypeKinds.GetParticularTypeKind(kind))
        //			{
        //				case TypeKind.DateTime:
        //				case TypeKind.Decimal:
        //				case TypeKind.Guid:
        //				case TypeKind.TimeSpan:
        //					image.Source = ((SWC.Image)Application.Current.FindResource("PrimitivePng")).Source;
        //					break;
        //				default:
        //					image.Source = ((SWC.Image)Application.Current.FindResource("StructPng")).Source;
        //					break;
        //			}
        //			break;
        //		case TypeKind.ReferenceKind:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("ClassPng")).Source;
        //			break;
        //		case TypeKind.EnumKind:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("EnumPng")).Source;
        //			break;
        //		case TypeKind.PrimitiveKind:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("PrimitivePng")).Source;
        //			break;
        //		default:
        //			image.Source = ((SWC.Image)Application.Current.FindResource("QuestionPng")).Source;
        //			break;
        //	}

        //	stackPanel.Children.Add(image);
        //	stackPanel.Children.Add(GuiUtils.GetClrtDisplayableTypeTextBlock(dispType));
        //	return stackPanel;
        //}


        #endregion Utils
    }


}
