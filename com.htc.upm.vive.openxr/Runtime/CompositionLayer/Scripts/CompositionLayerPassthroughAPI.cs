// Copyright HTC Corporation All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using VIVE.OpenXR.CompositionLayer;
using VIVE.OpenXR.CompositionLayer.Passthrough;

namespace VIVE.OpenXR.CompositionLayer.Passthrough
{
	[Obsolete("This class is deprecated. Please use PassthroughAPI instead.")]
	public static class CompositionLayerPassthroughAPI
	{
		const string LOG_TAG = "CompositionLayerPassthroughAPI";
		static void DEBUG(string msg) { Debug.Log(LOG_TAG + " " + msg); }
		static void WARNING(string msg) { Debug.LogWarning(LOG_TAG + " " + msg); }
		static void ERROR(string msg) { Debug.LogError(LOG_TAG + " " + msg); }

		private static ViveCompositionLayerPassthrough passthroughFeature = null;
		private static bool checkPassthroughFeatureInstance()
		{
			passthroughFeature = OpenXRSettings.Instance.GetFeature<ViveCompositionLayerPassthrough>();

			if (!passthroughFeature) return false;

			return true;
		}
#if UNITY_STANDALONE
		private static Dictionary<int, XrCompositionLayerPassthroughHTC> passthrough2Layer = new Dictionary<int, XrCompositionLayerPassthroughHTC>();
		private static Dictionary<int, IntPtr> passthrough2LayerPtr = new Dictionary<int, IntPtr>();
		private static Dictionary<int, bool> passthrough2IsUnderLay= new Dictionary<int, bool>();
		private static Dictionary<int, XrPassthroughMeshTransformInfoHTC> passthrough2meshTransform = new Dictionary<int, XrPassthroughMeshTransformInfoHTC>();
		private static Dictionary<int, IntPtr> passthrough2meshTransformInfoPtr = new Dictionary<int, IntPtr>();
#endif
		#region Public APIs

		/// <summary>
		/// For creating a fullscreen passthrough.
		/// Passthroughs will be destroyed automatically when the current XrSession is destroyed.
		/// </summary>
		/// <returns>
		/// ID of the created passthrough.
		/// Value will be 0 if passthrough is not created successfully.
		/// </returns>
		/// <param name="layerType">
		/// Specify whether the passthrough is an overlay or underlay. See <see cref="LayerType"/> for details.
		/// </param>
		/// <param name="onDestroyPassthroughSessionHandler">
		/// Delegate of type <see cref="ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate">OnPassthroughSessionDestroyDelegate</see>.
		/// This delegate will be invoked when the current OpenXR Session is going to be destroyed, which is when the created passthrough layer should be destroyed if not.
		/// </param>
		/// <param name="alpha">
		/// Specify the alpha of the passthrough layer.
		/// Should be within range [0, 1]
		/// 1 (Opaque) by default.
		/// </param>
		/// <param name="compositionDepth">
		/// Specify the composition depth relative to other composition layers if present.
		/// 0 by default.
		/// </param>
		public static int CreatePlanarPassthrough(LayerType layerType, ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate onDestroyPassthroughSessionHandler = null, float alpha = 1f, uint compositionDepth = 0)
		{
			int passthroughID = 0;

			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return passthroughID;
			}
#if UNITY_ANDROID
			passthroughID = passthroughFeature.HTCPassthrough_CreatePassthrough(layerType, PassthroughLayerForm.Planar, onDestroyPassthroughSessionHandler, compositionDepth);
#endif
#if UNITY_STANDALONE
			XrPassthroughHTC passthrough = 0;
			XrPassthroughCreateInfoHTC createInfo = new XrPassthroughCreateInfoHTC(
				XrStructureType.XR_TYPE_PASSTHROUGH_CREATE_INFO_HTC,
				new IntPtr(6), //Enter IntPtr(0) for backward compatibility (using createPassthrough to enable the passthrough feature), or enter IntPtr(6) to enable the passthrough feature based on the layer submitted to endframe.
				XrPassthroughFormHTC.XR_PASSTHROUGH_FORM_PLANAR_HTC
			);
			XrResult res = passthroughFeature.CreatePassthroughHTC(createInfo, out passthrough);
			if(res == XrResult.XR_SUCCESS)
            {
				ulong passthrough_ulong = passthrough;
				passthroughID = (int)passthrough_ulong;
				XrPassthroughColorHTC passthroughColor = new XrPassthroughColorHTC(
						in_type: XrStructureType.XR_TYPE_PASSTHROUGH_COLOR_HTC,
						in_next: IntPtr.Zero,
						in_alpha: alpha);
				XrCompositionLayerPassthroughHTC compositionLayerPassthrough = new XrCompositionLayerPassthroughHTC(
						in_type: XrStructureType.XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_HTC,
						in_next: IntPtr.Zero,
						in_layerFlags: ViveCompositionLayerHelper.XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT,
						in_space: 0,
						in_passthrough: passthrough,
						in_color: passthroughColor);
				passthrough2Layer.Add(passthroughID, compositionLayerPassthrough);
				IntPtr layerPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XrCompositionLayerPassthroughHTC)));
				passthrough2LayerPtr.Add(passthroughID, layerPtr);
				if (layerType == LayerType.Underlay)
					passthrough2IsUnderLay.Add(passthroughID, true);
				if (layerType == LayerType.Overlay)
					passthrough2IsUnderLay.Add(passthroughID, false);
			}
#endif
			if (passthroughID == 0)
			{
				ERROR("Failed to create projected pasthrough");
			}
			else
			{
				SetPassthroughAlpha(passthroughID, alpha);
			}

			return passthroughID;
		}

		/// <summary>
		/// For creating a projected passthrough (i.e. Passthrough is only partially visible).
		/// Visible region of the projected passthrough is determined by the mesh and its transform.
		/// Passthroughs will be destroyed automatically when the current XrSession is destroyed.
		/// </summary>
		/// <returns>
		/// ID of the created passthrough.
		/// Value will be 0 if passthrough is not created successfully.
		/// </returns>
		/// <param name="layerType">
		/// Specify whether the passthrough is an overlay or underlay. See <see cref="LayerType"/> for details.
		/// </param>
		/// <param name="vertexBuffer">
		/// Positions of the vertices in the mesh.
		/// </param>
		///<param name="indexBuffer">
		/// List of triangles represented by indices into the <paramref name="vertexBuffer"/>.
		/// </param>
		/// <param name="spaceType">
		/// Specify the type of space the projected passthrough is in. See <see cref="ProjectedPassthroughSpaceType"/> for details.
		/// </param>
		/// <param name="meshPosition">
		/// Position of the mesh.
		/// </param>
		/// <param name="meshOrientation">
		/// Orientation of the mesh.
		/// </param>
		/// <param name="meshScale">
		/// Scale of the mesh.
		/// </param>
		/// <param name="onDestroyPassthroughSessionHandler">
		/// Delegate of type <see cref="ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate">OnPassthroughSessionDestroyDelegate</see>.
		/// This delegate will be invoked when the current OpenXR Session is going to be destroyed, which is when the created passthrough layer should be destroyed if not.
		/// </param>
		/// <param name="alpha">
		/// Specify the alpha of the passthrough layer.
		/// Should be within range [0, 1]
		/// 1 (Opaque) by default.
		/// </param>
		/// <param name="compositionDepth">
		/// Specify the composition depth relative to other composition layers if present.
		/// 0 by default.
		/// </param>
		/// <param name="trackingToWorldSpace">
		/// Specify whether or not the position and rotation of the mesh transform have to be converted from tracking space to world space.
		/// </param>
		/// <param name="convertFromUnityToOpenXR">
		/// Specify whether the parameters
		/// <paramref name="vertexBuffer"/>, <paramref name="indexBuffer"/>, <paramref name="meshPosition"/> and <paramref name="meshOrientation"/> have to be converted for OpenXR.
		/// </param>
		public static int CreateProjectedPassthrough(LayerType layerType,
											  [In, Out] Vector3[] vertexBuffer, [In, Out] int[] indexBuffer, //For Mesh
											  ProjectedPassthroughSpaceType spaceType, Vector3 meshPosition, Quaternion meshOrientation, Vector3 meshScale, //For Mesh Transform
											  ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate onDestroyPassthroughSessionHandler = null,
											  float alpha = 1f, uint compositionDepth = 0, bool trackingToWorldSpace = true, bool convertFromUnityToOpenXR = true)
		{
			int passthroughID = 0;

			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return passthroughID;
			}

			if (vertexBuffer.Length < 3 || indexBuffer.Length % 3 != 0) //Must have at least 3 vertices and complete triangles
			{
				ERROR("Mesh data invalid.");
				return passthroughID;
			}
#if UNITY_STANDALONE
			XrPassthroughHTC passthrough = 0;
			XrPassthroughCreateInfoHTC createInfo = new XrPassthroughCreateInfoHTC(
				XrStructureType.XR_TYPE_PASSTHROUGH_CREATE_INFO_HTC,
				new IntPtr(6), //Enter IntPtr(0) for backward compatibility (using createPassthrough to enable the passthrough feature), or enter IntPtr(6) to enable the passthrough feature based on the layer submitted to endframe.
				XrPassthroughFormHTC.XR_PASSTHROUGH_FORM_PROJECTED_HTC
			);
			XrResult res = passthroughFeature.CreatePassthroughHTC(createInfo, out passthrough);
			if (res == XrResult.XR_SUCCESS)
			{
				ulong passthrough_ulong = passthrough;
				passthroughID = (int)passthrough_ulong;
				XrPassthroughMeshTransformInfoHTC PassthroughMeshTransformInfo = new XrPassthroughMeshTransformInfoHTC(
						in_type: XrStructureType.XR_TYPE_PASSTHROUGH_MESH_TRANSFORM_INFO_HTC,
						in_next: IntPtr.Zero,
						in_vertexCount: 0,
						in_vertices: new XrVector3f[0],
						in_indexCount: 0,
						in_indices: new UInt32[0],
						in_baseSpace: XR_HTC_passthrough.Interop.GetTrackingSpace(),
						in_time: XR_HTC_passthrough.Interop.GetFrameState().predictedDisplayTime,
						in_pose: new XrPosef(),
						in_scale: new XrVector3f()
						);
				IntPtr meshTransformInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XrPassthroughMeshTransformInfoHTC)));
				Marshal.StructureToPtr(PassthroughMeshTransformInfo, meshTransformInfoPtr, false);
				XrPassthroughColorHTC passthroughColor = new XrPassthroughColorHTC(
						in_type: XrStructureType.XR_TYPE_PASSTHROUGH_COLOR_HTC,
						in_next: IntPtr.Zero,
						in_alpha: alpha);
				XrCompositionLayerPassthroughHTC compositionLayerPassthrough = new XrCompositionLayerPassthroughHTC(
						in_type: XrStructureType.XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_HTC,
						in_next: meshTransformInfoPtr,
						in_layerFlags: ViveCompositionLayerHelper.XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT,
						in_space: 0,
						in_passthrough: passthrough,
						in_color: passthroughColor);
				passthrough2meshTransform.Add(passthroughID, PassthroughMeshTransformInfo);
				passthrough2meshTransformInfoPtr.Add(passthroughID, meshTransformInfoPtr);
				passthrough2Layer.Add(passthroughID, compositionLayerPassthrough);
				IntPtr layerPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XrCompositionLayerPassthroughHTC)));
				passthrough2LayerPtr.Add(passthroughID, layerPtr);
				if (layerType == LayerType.Underlay)
					passthrough2IsUnderLay.Add(passthroughID, true);
				if (layerType == LayerType.Overlay)
					passthrough2IsUnderLay.Add(passthroughID, false);
			}
#endif
#if UNITY_ANDROID
			passthroughID = passthroughFeature.HTCPassthrough_CreatePassthrough(layerType, PassthroughLayerForm.Projected, onDestroyPassthroughSessionHandler, compositionDepth);
#endif
			if (passthroughID == 0)
			{
				ERROR("Failed to create projected pasthrough");
			}
			else
			{
				SetPassthroughAlpha(passthroughID, alpha);
				SetProjectedPassthroughMesh(passthroughID, vertexBuffer, indexBuffer, convertFromUnityToOpenXR);
				SetProjectedPassthroughMeshTransform(passthroughID, spaceType, meshPosition, meshOrientation, meshScale, trackingToWorldSpace, convertFromUnityToOpenXR);
			}

			return passthroughID;
		}

		/// <summary>
		/// Creating a projected passthrough (i.e. Passthrough is only partially visible).
		/// Visible region of the projected passthrough is determined by the mesh and its transform.
		/// Passthroughs will be destroyed automatically when the current XrSession is destroyed.
		/// </summary>
		/// <remarks>
		/// When using this overload, <see cref="SetProjectedPassthroughMesh"/> and <see cref="SetProjectedPassthroughMeshTransform"/> must be called afterwards immediately.
		/// </remarks>
		/// <example>
		/// <code>
		/// int PassthroughID = CompositionLayerPassthroughAPI.CreateProjectedPassthrough(layerType, passthroughSessionDestroyHandler, alpha);
		/// CompositionLayerPassthroughAPI.SetProjectedPassthroughMesh(PassthroughID, quadVertices, quadIndicies, true);
		/// CompositionLayerPassthroughAPI.SetProjectedPassthroughMeshTranform(PassthroughID, spaceType, position, rotation, scale, true);
		/// </code>
		/// </example>
		/// <returns>
		/// ID of the created passthrough.
		/// Value will be 0 if passthrough is not created successfully.
		/// </returns>
		/// <param name="layerType">
		/// Specify whether the passthrough is an overlay or underlay. See <see cref="LayerType"/> for details.
		/// </param>
		/// <param name="onDestroyPassthroughSessionHandler">
		/// Delegate of type <see cref="ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate">OnPassthroughSessionDestroyDelegate</see>.
		/// This delegate will be invoked when the current OpenXR Session is going to be destroyed, which is when the created passthrough layer should be destroyed if not.
		/// </param>
		/// <param name="alpha">
		/// Specify the alpha of the passthrough layer.
		/// Should be within range [0, 1].
		/// 1 (Opaque) by default.
		/// </param>
		/// <param name="compositionDepth">
		/// Specify the composition depth relative to other composition layers if present.
		/// 0 by default.
		/// </param>
		public static int CreateProjectedPassthrough(LayerType layerType, ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate onDestroyPassthroughSessionHandler = null, float alpha = 1f, uint compositionDepth = 0)
		{
			int passthroughID = 0;

			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return passthroughID;
			}
#if UNITY_STANDALONE
			XrPassthroughHTC passthrough = 0;
			XrPassthroughCreateInfoHTC createInfo = new XrPassthroughCreateInfoHTC(
				XrStructureType.XR_TYPE_PASSTHROUGH_CREATE_INFO_HTC,
				new IntPtr(6), //Enter IntPtr(0) for backward compatibility (using createPassthrough to enable the passthrough feature), or enter IntPtr(6) to enable the passthrough feature based on the layer submitted to endframe.
				XrPassthroughFormHTC.XR_PASSTHROUGH_FORM_PROJECTED_HTC
			);
			XrResult res = passthroughFeature.CreatePassthroughHTC(createInfo, out passthrough);
			if (res == XrResult.XR_SUCCESS)
			{
				ulong passthrough_ulong = passthrough;
				passthroughID = (int)passthrough_ulong;
				XrPassthroughMeshTransformInfoHTC PassthroughMeshTransformInfo = new XrPassthroughMeshTransformInfoHTC(
						in_type: XrStructureType.XR_TYPE_PASSTHROUGH_MESH_TRANSFORM_INFO_HTC,
						in_next: IntPtr.Zero,
						in_vertexCount: 0,
						in_vertices: new XrVector3f[0],
						in_indexCount: 0,
						in_indices: new UInt32[0],
						in_baseSpace: XR_HTC_passthrough.Interop.GetTrackingSpace(),
						in_time: XR_HTC_passthrough.Interop.GetFrameState().predictedDisplayTime,
						in_pose: new XrPosef(),
						in_scale: new XrVector3f()
						);
				IntPtr meshTransformInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XrPassthroughMeshTransformInfoHTC)));
				Marshal.StructureToPtr(PassthroughMeshTransformInfo, meshTransformInfoPtr, false);
				XrPassthroughColorHTC passthroughColor = new XrPassthroughColorHTC(
						in_type: XrStructureType.XR_TYPE_PASSTHROUGH_COLOR_HTC,
						in_next: IntPtr.Zero,
						in_alpha: alpha);
				XrCompositionLayerPassthroughHTC compositionLayerPassthrough = new XrCompositionLayerPassthroughHTC(
						in_type: XrStructureType.XR_TYPE_COMPOSITION_LAYER_PASSTHROUGH_HTC,
						in_next: meshTransformInfoPtr,
						in_layerFlags: ViveCompositionLayerHelper.XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT,
						in_space: 0,
						in_passthrough: passthrough,
						in_color: passthroughColor);
				passthrough2meshTransform.Add(passthroughID, PassthroughMeshTransformInfo);
				passthrough2meshTransformInfoPtr.Add(passthroughID, meshTransformInfoPtr);
				passthrough2Layer.Add(passthroughID, compositionLayerPassthrough);
				IntPtr layerPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XrCompositionLayerPassthroughHTC)));
				passthrough2LayerPtr.Add(passthroughID, layerPtr);
				if (layerType == LayerType.Underlay)
					passthrough2IsUnderLay.Add(passthroughID, true);
				if (layerType == LayerType.Overlay)
					passthrough2IsUnderLay.Add(passthroughID, false);
			}
#endif
#if UNITY_ANDROID
			passthroughID = passthroughFeature.HTCPassthrough_CreatePassthrough(layerType, PassthroughLayerForm.Projected, onDestroyPassthroughSessionHandler);
#endif
			if (passthroughID == 0)
			{
				ERROR("Failed to create projected pasthrough");
			}
			else
			{
				SetPassthroughAlpha(passthroughID, alpha);
			}

			return passthroughID;
		}
#if UNITY_STANDALONE
		private static void SubmitLayer()
        {
			XR_HTC_passthrough.Interop.GetOriginEndFrameLayerList(out List<IntPtr> layerList);//GetOriginEndFrameLayers
			foreach(var passthrough in passthrough2IsUnderLay.Keys)
            {
				//Get and submit layer list
				if (layerList.Count != 0)
				{
					Marshal.StructureToPtr(passthrough2Layer[passthrough], passthrough2LayerPtr[passthrough], false);
					if (passthrough2IsUnderLay[passthrough])
						layerList.Insert(0, passthrough2LayerPtr[passthrough]);
					else
						layerList.Insert(1, passthrough2LayerPtr[passthrough]);
				}
			}
			XR_HTC_passthrough.Interop.SubmitLayers(layerList);
		}
#endif

		/// <summary>
		/// For destroying a passthrough created previously.
		/// This function should be called in the delegate instance of type <see cref="ViveCompositionLayerPassthrough.OnPassthroughSessionDestroyDelegate">OnPassthroughSessionDestroyDelegate</see> that is previously assigned when creating a passthrough.
		/// </summary>
		/// <returns>
		/// True for successfully destroying the specified passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the passthrough to be destroyed.
		/// </param>
		public static bool DestroyPassthrough(int passthroughID)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}
            if (!passthroughFeature.PassthroughIDList.Contains(passthroughID))
            {
                ERROR("Passthrough to be destroyed not found");
                return false;
            }
#if UNITY_STANDALONE
            XrPassthroughHTC passthrough = passthrough2Layer[passthroughID].passthrough;
			passthroughFeature.DestroyPassthroughHTC(passthrough);
			passthrough2IsUnderLay.Remove(passthroughID);
			SubmitLayer();
			passthrough2Layer.Remove(passthroughID);
			if(passthrough2LayerPtr.ContainsKey(passthroughID)) Marshal.FreeHGlobal(passthrough2LayerPtr[passthroughID]);
			passthrough2LayerPtr.Remove(passthroughID);
			if(passthrough2meshTransformInfoPtr.ContainsKey(passthroughID)) Marshal.FreeHGlobal(passthrough2meshTransformInfoPtr[passthroughID]);
			passthrough2meshTransformInfoPtr.Remove(passthroughID);
			passthrough2meshTransform.Remove(passthroughID);
			
			return true;
#endif
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_DestroyPassthrough(passthroughID);
#endif
		}

		/// <summary>
		/// For modifying the opacity of a specific passthrough layer.
		/// Can be used for both Planar and Projected passthroughs.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the opacity the specified passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="alpha">
		/// Specify the alpha of the passthrough layer.
		/// Should be within range [0, 1]
		/// 1 (Opaque) by default.
		/// </param>
		/// <param name="autoClamp">
		/// Specify whether out of range alpha values should be clamped automatically.
		/// When set to true, the function will clamp and apply the alpha value automatically.
		/// When set to false, the function will return false if the alpha is out of range.
		/// Set to true by default.
		/// </param>
		public static bool SetPassthroughAlpha(int passthroughID, float alpha, bool autoClamp = true)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}
#if UNITY_ANDROID
			if (autoClamp)
			{
				return passthroughFeature.HTCPassthrough_SetAlpha(passthroughID, Mathf.Clamp01(alpha));
			}
			else
			{
				if (alpha < 0f || alpha > 1f)
				{
					ERROR("SetPassthroughAlpha: Alpha out of range");
					return false;
				}

				return passthroughFeature.HTCPassthrough_SetAlpha(passthroughID, alpha);
			}
#endif
#if UNITY_STANDALONE
			if (passthrough2Layer.ContainsKey(passthroughID))
			{
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.color.alpha = alpha;
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
		}

		/// <summary>
		/// For modifying the mesh data of a projected passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the mesh data of the projected passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="vertexBuffer">
		/// Positions of the vertices in the mesh.
		/// </param>
		///<param name="indexBuffer">
		/// List of triangles represented by indices into the <paramref name="vertexBuffer"/>.
		/// </param>
		/// <param name="convertFromUnityToOpenXR">
		/// Specify whether the parameters
		/// <paramref name="vertexBuffer"/> and <paramref name="indexBuffer"/> have to be converted for OpenXR.
		/// </param>
		public static bool SetProjectedPassthroughMesh(int passthroughID, [In, Out] Vector3[] vertexBuffer, [In, Out] int[] indexBuffer, bool convertFromUnityToOpenXR = true)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}

			if (vertexBuffer.Length < 3 || indexBuffer.Length % 3 != 0) //Must have at least 3 vertices and complete triangles
			{
				ERROR("Mesh data invalid.");
				return false;
			}

			XrVector3f[] vertexBufferXrVector = new XrVector3f[vertexBuffer.Length];

			for (int i = 0; i < vertexBuffer.Length; i++)
			{
				vertexBufferXrVector[i] = OpenXRHelper.ToOpenXRVector(vertexBuffer[i], convertFromUnityToOpenXR);
			}

			uint[] indexBufferUint = new uint[indexBuffer.Length];

			for (int i = 0; i < indexBuffer.Length; i++)
			{
				indexBufferUint[i] = (uint)indexBuffer[i];
			}
#if UNITY_STANDALONE
			if (passthrough2meshTransformInfoPtr.ContainsKey(passthroughID))
			{
				XrPassthroughMeshTransformInfoHTC MeshTransformInfo = passthrough2meshTransform[passthroughID];
				MeshTransformInfo.vertexCount = (uint)vertexBuffer.Length;
				MeshTransformInfo.vertices = vertexBufferXrVector;
				MeshTransformInfo.indexCount = (uint)indexBuffer.Length;
				MeshTransformInfo.indices = indexBufferUint;
				passthrough2meshTransform[passthroughID] = MeshTransformInfo;
				Marshal.StructureToPtr(MeshTransformInfo, passthrough2meshTransformInfoPtr[passthroughID], false);
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.next = passthrough2meshTransformInfoPtr[passthroughID];
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
			//Note: Ignore Clock-Wise definition of index buffer for now as passthrough extension does not have back-face culling
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetMesh(passthroughID, (uint)vertexBuffer.Length, vertexBufferXrVector, (uint)indexBuffer.Length, indexBufferUint); ;
#endif
		}

		/// <summary>
		/// For modifying the mesh transform of a projected passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the mesh data of the projected passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="spaceType">
		/// Specify the type of space the projected passthrough is in. See <see cref="ProjectedPassthroughSpaceType"/> for details.
		/// </param>
		/// <param name="meshPosition">
		/// Position of the mesh.
		/// </param>
		/// <param name="meshOrientation">
		/// Orientation of the mesh.
		/// </param>
		/// <param name="meshScale">
		/// Scale of the mesh.
		/// </param>
		/// <param name="trackingToWorldSpace">
		/// Specify whether or not the position and rotation of the mesh transform have to be converted from tracking space to world space.
		/// </param>
		/// <param name="convertFromUnityToOpenXR">
		/// Specify whether the parameters
		/// <paramref name="meshPosition"/> and <paramref name="meshOrientation"/> have to be converted for OpenXR.
		/// </param>
		public static bool SetProjectedPassthroughMeshTransform(int passthroughID, ProjectedPassthroughSpaceType spaceType, Vector3 meshPosition, Quaternion meshOrientation, Vector3 meshScale, bool trackingToWorldSpace = true, bool convertFromUnityToOpenXR = true)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}

			Vector3 trackingSpaceMeshPosition = meshPosition;
			Quaternion trackingSpaceMeshRotation = meshOrientation;
			TrackingSpaceOrigin currentTrackingSpaceOrigin = TrackingSpaceOrigin.Instance;

			if (currentTrackingSpaceOrigin != null && trackingToWorldSpace) //Apply origin correction to the mesh pose
			{
				Matrix4x4 trackingSpaceOriginTRS = Matrix4x4.TRS(currentTrackingSpaceOrigin.transform.position, currentTrackingSpaceOrigin.transform.rotation, Vector3.one);
				Matrix4x4 worldSpaceLayerPoseTRS = Matrix4x4.TRS(meshPosition, meshOrientation, Vector3.one);

				Matrix4x4 trackingSpaceLayerPoseTRS = trackingSpaceOriginTRS.inverse * worldSpaceLayerPoseTRS;

				trackingSpaceMeshPosition = trackingSpaceLayerPoseTRS.GetColumn(3); //4th Column of TRS Matrix is the position
				trackingSpaceMeshRotation = Quaternion.LookRotation(trackingSpaceLayerPoseTRS.GetColumn(2), trackingSpaceLayerPoseTRS.GetColumn(1));
			}

			XrPosef meshXrPose;
			meshXrPose.position = OpenXRHelper.ToOpenXRVector(trackingSpaceMeshPosition, convertFromUnityToOpenXR);
			meshXrPose.orientation = OpenXRHelper.ToOpenXRQuaternion(trackingSpaceMeshRotation, convertFromUnityToOpenXR);

			XrVector3f meshXrScale = OpenXRHelper.ToOpenXRVector(meshScale, false);
#if UNITY_STANDALONE
			if (passthrough2meshTransformInfoPtr.ContainsKey(passthroughID))
			{
				XrPassthroughMeshTransformInfoHTC MeshTransformInfo = passthrough2meshTransform[passthroughID];
				MeshTransformInfo.pose = meshXrPose;
				MeshTransformInfo.scale = meshXrScale;
				passthrough2meshTransform[passthroughID] = MeshTransformInfo;
				Marshal.StructureToPtr(MeshTransformInfo, passthrough2meshTransformInfoPtr[passthroughID], false);
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.next = passthrough2meshTransformInfoPtr[passthroughID];
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetMeshTransform(passthroughID, passthroughFeature.GetXrSpaceFromSpaceType(spaceType), meshXrPose, meshXrScale);
#endif
		}

		/// <summary>
		/// For modifying layer type and composition depth of a passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the layer type and composition depth of the passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="layerType">
		/// Specify whether the passthrough is an overlay or underlay. See <see cref="LayerType"/> for details.
		/// </param>
		/// <param name="compositionDepth">
		/// Specify the composition depth relative to other composition layers if present.
		/// 0 by default.
		/// </param>
		public static bool SetPassthroughLayerType(int passthroughID, LayerType layerType, uint compositionDepth = 0)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}
#if UNITY_STANDALONE
			if (passthrough2IsUnderLay.ContainsKey(passthroughID))
			{
				passthrough2IsUnderLay[passthroughID] = layerType == LayerType.Underlay ? true : false;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetLayerType(passthroughID, layerType, compositionDepth);
#endif

		}

		/// <summary>
		/// For modifying the space of a projected passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the space of the projected passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="spaceType">
		/// Specify the type of space the projected passthrough is in. See <see cref="ProjectedPassthroughSpaceType"/> for details.
		/// </param>
		public static bool SetProjectedPassthroughSpaceType(int passthroughID, ProjectedPassthroughSpaceType spaceType)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}
#if UNITY_STANDALONE
			if (passthrough2meshTransformInfoPtr.ContainsKey(passthroughID))
			{
				XrPassthroughMeshTransformInfoHTC MeshTransformInfo = passthrough2meshTransform[passthroughID];
				MeshTransformInfo.baseSpace = passthroughFeature.GetXrSpaceFromSpaceType(spaceType);
				passthrough2meshTransform[passthroughID] = MeshTransformInfo;
				Marshal.StructureToPtr(MeshTransformInfo, passthrough2meshTransformInfoPtr[passthroughID], false);
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.next = passthrough2meshTransformInfoPtr[passthroughID];
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetMeshTransformSpace(passthroughID, passthroughFeature.GetXrSpaceFromSpaceType(spaceType));
#endif
		}

		/// <summary>
		/// For modifying the mesh position of a projected passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the mesh position of the projected passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="meshPosition">
		/// Position of the mesh.
		/// </param>
		/// <param name="trackingToWorldSpace">
		/// Specify whether or not the position of the mesh transform have to be converted from tracking space to world space.
		/// </param>
		/// <param name="convertFromUnityToOpenXR">
		/// Specify whether the parameter
		/// <paramref name="meshPosition"/> have to be converted for OpenXR.
		/// </param>
		public static bool SetProjectedPassthroughMeshPosition(int passthroughID, Vector3 meshPosition, bool trackingToWorldSpace = true, bool convertFromUnityToOpenXR = true)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}

			Vector3 trackingSpaceMeshPosition = meshPosition;
			TrackingSpaceOrigin currentTrackingSpaceOrigin = TrackingSpaceOrigin.Instance;

			if (currentTrackingSpaceOrigin != null && trackingToWorldSpace) //Apply origin correction to the mesh pose
			{
				Matrix4x4 trackingSpaceOriginTRS = Matrix4x4.TRS(currentTrackingSpaceOrigin.transform.position, Quaternion.identity, Vector3.one);
				Matrix4x4 worldSpaceLayerPoseTRS = Matrix4x4.TRS(meshPosition, Quaternion.identity, Vector3.one);

				Matrix4x4 trackingSpaceLayerPoseTRS = trackingSpaceOriginTRS.inverse * worldSpaceLayerPoseTRS;

				trackingSpaceMeshPosition = trackingSpaceLayerPoseTRS.GetColumn(3); //4th Column of TRS Matrix is the position
			}
#if UNITY_STANDALONE
			if (passthrough2meshTransformInfoPtr.ContainsKey(passthroughID))
			{
				XrPassthroughMeshTransformInfoHTC MeshTransformInfo = passthrough2meshTransform[passthroughID];
				XrPosef meshXrPose = MeshTransformInfo.pose;
				meshXrPose.position = OpenXRHelper.ToOpenXRVector(trackingSpaceMeshPosition, convertFromUnityToOpenXR); ;
				MeshTransformInfo.pose = meshXrPose;
				passthrough2meshTransform[passthroughID] = MeshTransformInfo;
				Marshal.StructureToPtr(MeshTransformInfo, passthrough2meshTransformInfoPtr[passthroughID], false);
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.next = passthrough2meshTransformInfoPtr[passthroughID];
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetMeshTransformPosition(passthroughID, OpenXRHelper.ToOpenXRVector(trackingSpaceMeshPosition, convertFromUnityToOpenXR));
#endif
		}

		/// <summary>
		/// For modifying the mesh orientation of a projected passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the mesh orientation of the projected passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="meshOrientation">
		/// Orientation of the mesh.
		/// </param>
		/// <param name="trackingToWorldSpace">
		/// Specify whether or not the rotation of the mesh transform have to be converted from tracking space to world space.
		/// </param>
		/// <param name="convertFromUnityToOpenXR">
		/// Specify whether the parameter
		/// <paramref name="meshOrientation"/> have to be converted for OpenXR.
		/// </param>
		public static bool SetProjectedPassthroughMeshOrientation(int passthroughID, Quaternion meshOrientation, bool trackingToWorldSpace = true, bool convertFromUnityToOpenXR = true)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}

			Quaternion trackingSpaceMeshRotation = meshOrientation;
			TrackingSpaceOrigin currentTrackingSpaceOrigin = TrackingSpaceOrigin.Instance;

			if (currentTrackingSpaceOrigin != null && trackingToWorldSpace) //Apply origin correction to the mesh pose
			{
				Matrix4x4 trackingSpaceOriginTRS = Matrix4x4.TRS(Vector3.zero, currentTrackingSpaceOrigin.transform.rotation, Vector3.one);
				Matrix4x4 worldSpaceLayerPoseTRS = Matrix4x4.TRS(Vector3.zero, meshOrientation, Vector3.one);

				Matrix4x4 trackingSpaceLayerPoseTRS = trackingSpaceOriginTRS.inverse * worldSpaceLayerPoseTRS;

				trackingSpaceMeshRotation = Quaternion.LookRotation(trackingSpaceLayerPoseTRS.GetColumn(2), trackingSpaceLayerPoseTRS.GetColumn(1));
			}
#if UNITY_STANDALONE
			if (passthrough2meshTransformInfoPtr.ContainsKey(passthroughID))
			{
				XrPassthroughMeshTransformInfoHTC MeshTransformInfo = passthrough2meshTransform[passthroughID];
				XrPosef meshXrPose = MeshTransformInfo.pose;
				meshXrPose.orientation = OpenXRHelper.ToOpenXRQuaternion(trackingSpaceMeshRotation, convertFromUnityToOpenXR);
				MeshTransformInfo.pose = meshXrPose;
				passthrough2meshTransform[passthroughID] = MeshTransformInfo;
				Marshal.StructureToPtr(MeshTransformInfo, passthrough2meshTransformInfoPtr[passthroughID], false);
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.next = passthrough2meshTransformInfoPtr[passthroughID];
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif
#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetMeshTransformOrientation(passthroughID, OpenXRHelper.ToOpenXRQuaternion(trackingSpaceMeshRotation, convertFromUnityToOpenXR));
#endif
		}

		/// <summary>
		/// For modifying the mesh scale of a passthrough layer.
		/// </summary>
		/// <returns>
		/// True for successfully modifying the mesh data of the projected passthrough, vice versa.
		/// </returns>
		/// <param name="passthroughID">
		/// The ID of the target passthrough.
		/// </param>
		/// <param name="meshScale">
		/// Scale of the mesh.
		/// </param>
		public static bool SetProjectedPassthroughScale(int passthroughID, Vector3 meshScale)
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return false;
			}
#if UNITY_STANDALONE
			if (passthrough2meshTransformInfoPtr.ContainsKey(passthroughID))
			{
				XrPassthroughMeshTransformInfoHTC MeshTransformInfo = passthrough2meshTransform[passthroughID];
				MeshTransformInfo.scale = OpenXRHelper.ToOpenXRVector(meshScale, false);
				passthrough2meshTransform[passthroughID] = MeshTransformInfo;
				Marshal.StructureToPtr(MeshTransformInfo, passthrough2meshTransformInfoPtr[passthroughID], false);
				XrCompositionLayerPassthroughHTC layer = passthrough2Layer[passthroughID];
				layer.next = passthrough2meshTransformInfoPtr[passthroughID];
				passthrough2Layer[passthroughID] = layer;
				SubmitLayer();
				return true;
			}
			else
				return false;
#endif

#if UNITY_ANDROID
			return passthroughFeature.HTCPassthrough_SetMeshTransformScale(passthroughID, OpenXRHelper.ToOpenXRVector(meshScale, false));
#endif
		}

		/// <summary>
		/// To get the list of IDs of active passthrough layers.
		/// </summary>
		/// <returns>
		/// The a copy of the list of IDs of active passthrough layers.
		/// </returns>
		public static List<int> GetCurrentPassthroughLayerIDs()
		{
			if (!checkPassthroughFeatureInstance())
			{
				ERROR("HTC_Passthrough feature instance not found.");
				return null;
			}

			return passthroughFeature.PassthroughIDList;
		}
#endregion
	}
}