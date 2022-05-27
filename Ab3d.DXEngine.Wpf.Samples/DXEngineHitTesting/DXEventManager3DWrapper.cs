using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Media3D;
using Ab3d.DirectX;
using Ab3d.DirectX.Controls;
using Ab3d.DirectX.Models;
using Ab3d.Utilities;

namespace Ab3d.DXEngine.Wpf.Samples.DXEngineHitTesting
{
    // DXEventManager3DWrapper class is derived from the standard EventManager3D from Ab3d.PowerToys library.
    //
    // This means that when using DXEventManager3DWrapper, you can use the same methods and events as with EventManager3D
    // and use the much faster hit testing from DXEngine.
    //
    // Unfortunately for the DXEventManager3DWrapper to work, .Net's reflection needs to be used
    // to be able to create the RayMeshGeometry3DHitTestResult class and sets its properties.

    public class DXEventManager3DWrapper : Ab3d.Utilities.EventManager3D
    {
        private DXViewportView _dxViewportView;

        private static ConstructorInfo _rayMeshGeometry3DHitTestResultConstructor;
        private List<SceneNode> _sceneNodesToCheck;

        public bool IsUsingWpfHitTesting = false;


        public DXEventManager3DWrapper(DXViewportView dxViewportView)
            : base(dxViewportView.Viewport3D)
        {
            if (dxViewportView == null) throw new ArgumentNullException(nameof(dxViewportView));

            _dxViewportView = dxViewportView;

            this.CustomEventsSourceElement = dxViewportView;

            EnsureRayMeshGeometry3DHitTestResultConstructor();
        }

        protected override void CheckMouseOverElement(Point viewboxMousePosition, List<BaseEventSource3D> eventSources, List<Visual3D> excludedVisuals, bool checkOnlyDragSurfaces)
        {
            EnsureRayMeshGeometry3DHitTestResultConstructor();

            if (IsUsingWpfHitTesting)
            {
                base.CheckMouseOverElement(viewboxMousePosition, eventSources, excludedVisuals, checkOnlyDragSurfaces);
                return;
            }

            if (_dxViewportView.DXScene == null) // We need to wait until DXScene is initialized
                return;


            var pickRay = _dxViewportView.DXScene.GetRayFromCamera((int)viewboxMousePosition.X, (int)viewboxMousePosition.Y);


            DXRayHitTestResult dxRayHitTestResult = null;

            lastHitEventSource3D = null;
            lastRayHitResult = null;


            if (!checkOnlyDragSurfaces)
            {
                if (excludedVisuals != null)
                {
                    // Use filter to exclude Visual3D objects from excludedVisuals
                    _dxViewportView.DXScene.DXHitTestOptions.HitTestFilterCallback = delegate (SceneNode node)
                    {
                        var wpfModelVisual3DNode = node as WpfModelVisual3DNode;

                        if (wpfModelVisual3DNode != null)
                        {
                            for (var i = 0; i < excludedVisuals.Count; i++)
                            {
                                if (ReferenceEquals(wpfModelVisual3DNode.ModelVisual3D, excludedVisuals[i]))
                                    return DXHitTestOptions.HitTestFilterResult.ContinueSkipSelfAndChildren;
                            }
                        }
                        else
                        {
                            var wpfUiElement3DNode = node as WpfUIElement3DNode;
                            if (wpfUiElement3DNode != null)
                            {
                                for (var i = 0; i < excludedVisuals.Count; i++)
                                {
                                    if (ReferenceEquals(wpfUiElement3DNode.UIElement3D, excludedVisuals[i]))
                                        return DXHitTestOptions.HitTestFilterResult.ContinueSkipSelfAndChildren;
                                }
                            }
                        }

                        return DXHitTestOptions.HitTestFilterResult.Continue;
                    };
                }
                else
                {
                    _dxViewportView.DXScene.DXHitTestOptions.HitTestFilterCallback = null;
                }

                dxRayHitTestResult = _dxViewportView.DXScene.GetClosestHitObject(pickRay);


                if (dxRayHitTestResult != null)
                {
                    lastRayHitResult = CreateRayMeshGeometry3DHitTestResult(dxRayHitTestResult);

                    var visualHit = lastRayHitResult.VisualHit;

                    // First check if any TargetVisual3D was hit
                    if (visualHit != null)
                    {
                        for (int i = 0; i < eventSources.Count; i++)
                        {
                            BaseEventSource3D oneEventSource3D = eventSources[i];
                            if (oneEventSource3D.IsMyVisual(visualHit))
                            {
                                // Hit oneEventSource3D
                                lastHitEventSource3D = oneEventSource3D; // Get the hit object
                                break;
                            }
                        }
                    }

                    if (lastHitEventSource3D == null)
                    {
                        var modelHit = lastRayHitResult.ModelHit as GeometryModel3D;

                        if (modelHit != null)
                        {
                            int eventSourcesCount = eventSources.Count;
                            for (int i = 0; i < eventSourcesCount; i++) // use for instead of foreach to prevent creating new object in foreach
                            {
                                BaseEventSource3D oneEventSource3D = eventSources[i];

                                // First find the hit object (_lastHitObject)
                                if (lastHitEventSource3D == null)
                                {
                                    if (oneEventSource3D.IsMyGeometryModel3D(modelHit))
                                    {
                                        lastHitEventSource3D = oneEventSource3D;
                                    }
                                }
                            }
                        }
                    }
                }
            }


            if (lastHitEventSource3D == null && (dxRayHitTestResult != null || checkOnlyDragSurfaces))
            {
                // When there is is an object hit, but it is not one that is subscribed,
                // and there is an eventSources that is defined as a DragSource, we need to check each DragSurface if it is hit and found the closest

                // First collect all SceneNodes that need to be checked (SceneNodes that are created from DragSurfaces)
                if (_sceneNodesToCheck == null)
                    _sceneNodesToCheck = new List<SceneNode>(); // Reuse array so we do not create it on each hit test check


                BaseEventSource3D closestEventSource3D = null;
                DXRayHitTestResult closestRayHit = null;
                float closestDistanceToRayOrigin = float.MaxValue;


                for (var i = 0; i < eventSources.Count; i++)
                {
                    var eventSource = eventSources[i];

                    if (!eventSource.IsDragSurface)
                        continue;

                    var multiVisualEventSource3D = eventSource as MultiVisualEventSource3D;
                    if (multiVisualEventSource3D != null)
                    {
                        for (var i1 = 0; i1 < multiVisualEventSource3D.TargetVisuals3D.Count; i1++)
                        {
                            var sceneNode = _dxViewportView.GetSceneNodeForWpfObject(multiVisualEventSource3D.TargetVisuals3D[i1]);
                            if (sceneNode != null)
                                _sceneNodesToCheck.Add(sceneNode);
                        }
                    }
                    else
                    {
                        var visualEventSource3D = eventSource as VisualEventSource3D;
                        if (visualEventSource3D != null)
                        {
                            var sceneNode = _dxViewportView.GetSceneNodeForWpfObject(visualEventSource3D.TargetVisual3D);
                            if (sceneNode != null)
                                _sceneNodesToCheck.Add(sceneNode);
                        }
                    }


                    // Now check if SceneNodes intersect with the pickRay
                    for (var i1 = 0; i1 < _sceneNodesToCheck.Count; i1++)
                    {
                        var sceneNode = _sceneNodesToCheck[i1];

                        // We cannot test WpfModelVisual3DNode because it does not implement IRayHitTestedNode.
                        // Therefore we need to test its child WpfGeometryModel3DNode instead
                        var wpfModelVisual3DNode = sceneNode as WpfModelVisual3DNode;
                        if (wpfModelVisual3DNode != null && wpfModelVisual3DNode.ChildNodesCount == 1)
                            sceneNode = wpfModelVisual3DNode.ChildNodes[0];

                        var oneHitResult = _dxViewportView.DXScene.HitTestSceneNode(pickRay, sceneNode);

                        if (oneHitResult != null && oneHitResult.DistanceToRayOrigin < closestDistanceToRayOrigin)
                        {
                            // When we tested the ModelVisual3D, 
                            // we need to revert ModelVisual3D.Transform on HitPosition,
                            // because it is applied again in the MouseDrag3DEventArgs constructor 
                            if (wpfModelVisual3DNode != null && wpfModelVisual3DNode.Transform != null)
                            {
                                var matrix = wpfModelVisual3DNode.Transform.Value;
                                matrix.Invert();

                                var transformation = new Transformation(matrix);
                                transformation.Transform(ref oneHitResult.HitPosition, out oneHitResult.HitPosition);
                            }

                            closestEventSource3D = eventSource;
                            closestRayHit = oneHitResult;

                            closestDistanceToRayOrigin = oneHitResult.DistanceToRayOrigin;
                        }
                    }

                    _sceneNodesToCheck.Clear();
                }

                if (closestEventSource3D != null)
                {
                    lastHitEventSource3D = closestEventSource3D;
                    lastRayHitResult = CreateRayMeshGeometry3DHitTestResult(closestRayHit);
                }
            }
        }


        // UH: 
        // The RayMeshGeometry3DHitTestResult class that is passed in hit test results does not provide
        // a constructor that would take hit results are parameters.
        // Therefore we need to use reflection to create the RayMeshGeometry3DHitTestResult.
        private void EnsureRayMeshGeometry3DHitTestResultConstructor()
        {
            if (_rayMeshGeometry3DHitTestResultConstructor == null)
            {
                try
                {
                    _rayMeshGeometry3DHitTestResultConstructor = typeof(System.Windows.Media.Media3D.RayMeshGeometry3DHitTestResult).GetConstructor(
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { typeof(Visual3D), typeof(Model3D), typeof(MeshGeometry3D), typeof(Point3D), typeof(double), typeof(int), typeof(int), typeof(int), typeof(Point) },
                        null);
                }
                catch
                {
                }

                if (_rayMeshGeometry3DHitTestResultConstructor == null)
                    IsUsingWpfHitTesting = true; // Fallback to WPF hit testing because the constructor for the RayMeshGeometry3DHitTestResult has changed (practically not possible)
            }
        }

        private RayMeshGeometry3DHitTestResult CreateRayMeshGeometry3DHitTestResult(DXRayHitTestResult dxRayHitTestResult)
        {
            if (dxRayHitTestResult == null)
                return null;


            Visual3D visualHit;
            GeometryModel3D modelHit;
            MeshGeometry3D meshHit;
            int vertexIndex1, vertexIndex2, vertexIndex3;

            var wpfGeometryModel3DNode = dxRayHitTestResult.HitSceneNode as WpfGeometryModel3DNode;
            if (wpfGeometryModel3DNode != null)
            {
                modelHit = wpfGeometryModel3DNode.GeometryModel3D as GeometryModel3D;
                meshHit = wpfGeometryModel3DNode.DXMesh.MeshGeometry;

                int indiceIndex = dxRayHitTestResult.TriangleIndex * 3;

                vertexIndex1 = meshHit.TriangleIndices[indiceIndex];
                vertexIndex2 = meshHit.TriangleIndices[indiceIndex + 1];
                vertexIndex3 = meshHit.TriangleIndices[indiceIndex + 2];
            }
            else
            {
                modelHit = null;
                meshHit = null;

                vertexIndex1 = vertexIndex2 = vertexIndex3 = 0;
            }

            SceneNode currentNode = dxRayHitTestResult.HitSceneNode;
            while (currentNode != null && !(currentNode is WpfModelVisual3DNode) &&  !(currentNode is WpfUIElement3DNode))
            {
                currentNode = currentNode.ParentNode;
            }

            var wpfModelVisual3DNode = currentNode as WpfModelVisual3DNode;
            if (wpfModelVisual3DNode != null)
            {
                visualHit = wpfModelVisual3DNode.ModelVisual3D;
            }
            else
            {
                var wpfUiElement3DNode = currentNode as WpfUIElement3DNode;

                if (wpfUiElement3DNode != null)
                    visualHit = wpfUiElement3DNode.UIElement3D;
                else
                    visualHit = null;
            }

            var rayMeshGeometry3DHitTestResult = CreateRayMeshGeometry3DHitTestResult(
                    visualHit,
                    modelHit,
                    meshHit,
                    dxRayHitTestResult.HitPosition.ToWpfPoint3D(),
                    (double)dxRayHitTestResult.DistanceToRayOrigin,
                    vertexIndex1,
                    vertexIndex2,
                    vertexIndex3,
                    new Point(0, 0)); // barycentricCoordinate is not supported - user will need to calculate that by himself from triangle data if needed

            return rayMeshGeometry3DHitTestResult;
        }

        private RayMeshGeometry3DHitTestResult CreateRayMeshGeometry3DHitTestResult(Visual3D visualHit, Model3D modelHit, MeshGeometry3D meshHit, Point3D pointHit, double distanceToRayOrigin, int vertexIndex1, int vertexIndex2, int vertexIndex3, Point barycentricCoordinate)
        {
            EnsureRayMeshGeometry3DHitTestResultConstructor();

            if (_rayMeshGeometry3DHitTestResultConstructor != null)
            {
                var rayMeshGeometry3DHitTestResult = (RayMeshGeometry3DHitTestResult)_rayMeshGeometry3DHitTestResultConstructor.Invoke(new object[] {
                        visualHit, modelHit, meshHit, pointHit, distanceToRayOrigin, vertexIndex1, vertexIndex2, vertexIndex3, barycentricCoordinate });

                return rayMeshGeometry3DHitTestResult;
            }

            return null;
        }
    }
}