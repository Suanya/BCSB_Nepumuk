#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Niantic.ARDK.Utilities.Collections;
using Niantic.ARDK.Utilities.Editor;
using Niantic.ARDK.Utilities.Logging;
using Niantic.ARDK.VirtualStudio.AR.Mock;

using UnityEditor;
using UnityEngine;

namespace Niantic.ARDK.AR.WayspotAnchors
{
  [Serializable]
  public sealed class VPSLocationManifest: ScriptableObject
  {
    [SerializeField][HideInInspector]
    private string _nodeIdentifier;

    [SerializeField][HideInInspector]
    private AuthoredWayspotAnchorData[] _authoredAnchors;

    [SerializeField][HideInInspector]
    private string _mockAssetGuid;

    [SerializeField][HideInInspector]
    private string _jsonExportPath = "";

    private string _locationName;

    public string LocationName
    {
      get
      {
        if (string.IsNullOrEmpty(_locationName))
        {
          _locationName = Path.GetFileNameWithoutExtension( AssetDatabase.GetAssetPath(this));
        }

        return _locationName;
      }
      set
      {
        if (string.IsNullOrEmpty(value))
          throw new ArgumentNullException(nameof(value));

        if (string.Equals(_locationName, value))
          return;

        if (string.IsNullOrEmpty(_locationName))
        {
          _locationName = value;
          return;
        }

        var assetPath = AssetDatabase.GetAssetPath(this);

        var manifests = _AssetDatabaseUtilities.FindAssets<VPSLocationManifest>();
        if (manifests.Any(m => string.Equals(m.LocationName, value)))
        {
          ARLog._WarnRelease
          (
            $"Cannot rename location \'{_locationName}\'. " +
            $"A location named \'{value}\' already exists."
          );

          // If value was changed by in the Project Browser instead of the Inspector,
          // revert the name
          if (string.Equals(Path.GetFileNameWithoutExtension(assetPath), value))
          {
            // Without the delay, Project Browser won't display corrected name until after asset
            // is re-imported. Can't force asset re-import because of the name change, so a delay is
            // the solution.
            var oldName = _locationName + ".asset";
            EditorApplication.delayCall += () => AssetDatabase.RenameAsset(assetPath, oldName);
          }

          return;
        }

        _locationName = value;
        AssetDatabase.RenameAsset(assetPath, value + ".asset");
      }
    }

    private void OnValidate()
    {
      // TODO (kcho): Prevent duplicate names in different folders
      LocationName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(this));
    }

    internal string _NodeIdentifier
    {
      get { return _nodeIdentifier; }
      set
      {
        _nodeIdentifier = value;
        EditorUtility.SetDirty(this);
      }
    }

    internal string _JsonExportPath
    {
      get { return _jsonExportPath; }
      set
      {
        _jsonExportPath = value;
        EditorUtility.SetDirty(this);
      }
    }

    public IReadOnlyList<AuthoredWayspotAnchorData> AuthoredAnchorsData
    {
      get
      {
        return _authoredAnchors.AsNonNullReadOnly();
      }
    }

    private Dictionary<string, AuthoredWayspotAnchorData> _indexedAuthoredAnchors;

    internal bool _GetAnchorData(string identifier, out AuthoredWayspotAnchorData data)
    {
      if (_indexedAuthoredAnchors == null || _indexedAuthoredAnchors.Count != _authoredAnchors.Length)
      {
        _indexedAuthoredAnchors = new Dictionary<string, AuthoredWayspotAnchorData>();
        foreach (var anchor in _authoredAnchors)
        {
          _indexedAuthoredAnchors.Add(anchor._ManifestIdentifier, anchor);
        }
      }

      return _indexedAuthoredAnchors.TryGetValue(identifier, out data);
    }

    private UnityEngine.Mesh _mesh;
    public UnityEngine.Mesh Mesh
    {
      get
      {
        if (_mesh == null)
        {
          var assetPath = AssetDatabase.GetAssetPath(this);
          _mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(assetPath);

          if (_mesh == null)
            ARLog._Error($"No mesh found as sub-asset of {name} (VPSLocationManifest)");
        }

        return _mesh;
      }
    }

    private Material _material;
    public Material Material
    {
      get
      {
        if (_material == null)
        {
          var assetPath = AssetDatabase.GetAssetPath(this);
          _material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

          // Ideally the texture is already referenced by the material, but the material for some
          // reason loses the reference when saved.
          var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
          _material.mainTexture = tex;

          if (_material == null)
            ARLog._Error($"No material found as sub-asset of {name} (VPSLocationManifest)");
        }

        return _material;
      }
    }

    [SerializeField]
    private GameObject _mockAsset;

    internal GameObject _MockAsset
    {
      get
      {
        return _mockAsset;
      }
      set
      {
        _mockAsset = value;
        EditorUtility.SetDirty(this);
      }
    }

    internal void _CreateMockAsset()
    {
      var parentFolder = "ARDKMockEnvironments";
      var folder = "Wayspots";
      if (!AssetDatabase.IsValidFolder($"Assets/{parentFolder}"))
        AssetDatabase.CreateFolder("Assets", parentFolder);

      if (!AssetDatabase.IsValidFolder($"Assets/{parentFolder}/{folder}"))
        AssetDatabase.CreateFolder($"Assets/{parentFolder}", folder);

      // Create root
      var rootGo = new GameObject("Root");
      rootGo.AddComponent<MockSceneConfiguration>();
      rootGo.AddComponent<_TransformFixer>();

      // Create mesh and components
      var meshGo = new GameObject(LocationName + " (Wayspot Mesh)");
      var meshFilter = meshGo.AddComponent<MeshFilter>();
      meshFilter.sharedMesh = Mesh;

      var renderer = meshGo.AddComponent<MeshRenderer>();
      renderer.sharedMaterial = Material;

      meshGo.transform.SetParent(rootGo.transform, true);
      meshGo.AddComponent<_TransformFixer>();

      // Create MockWayspot component
      var mockWayspot = rootGo.AddComponent<MockWayspot>();
      mockWayspot._WayspotName = LocationName;
      mockWayspot._MeshObject = meshGo;
      mockWayspot._VPSLocationManifest = this;

      // Save asset
      var assetPath =
        _ProjectBrowserUtilities.BuildAssetPath
        (
          LocationName + ".prefab",
          $"Assets/{parentFolder}/{folder}"
        );

      var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath);
      _MockAsset = prefab;

      // Cleanup in scene
      GameObject.DestroyImmediate(rootGo);
    }

    public string ExportToJson()
    {
      var tinyManifest = new TinyVPSLocationManifest(this);
      return tinyManifest.ToJson();
    }

    internal AuthoredWayspotAnchorData _AddAnchorData
    (
      string anchorName,
      string manifestIdentifier = null,
      string anchorIdentifier = null,
      string payload = null,
      Vector3? position = null,
      Vector3? rotation = null,
      Vector3? scale = null,
      string tags = null,
      AuthoredWayspotAnchorData.PrefabData[] prefabs = null
    )
    {
      if (prefabs != null)
      {
        var prefabsCopy = new AuthoredWayspotAnchorData.PrefabData[prefabs.Length];
        prefabsCopy = prefabs.Select(p => p.Copy()).ToArray();
        prefabs = prefabsCopy;
      }
      else
      {
        prefabs = new AuthoredWayspotAnchorData.PrefabData[0];
      }

      var data =
        new AuthoredWayspotAnchorData
        (
          anchorName,
          anchorIdentifier,
          payload,
          position.HasValue ? position.Value : Vector3.zero,
          rotation.HasValue ? rotation.Value : Vector3.zero,
          scale.HasValue ? scale.Value : Vector3.one,
          tags,
          prefabs,
          string.IsNullOrEmpty(manifestIdentifier) ? Guid.NewGuid().ToString() : manifestIdentifier
        );

      if (_authoredAnchors == null)
      {
        _authoredAnchors = new AuthoredWayspotAnchorData[] { data };
      }
      else
      {
        var oldAnchors = _authoredAnchors;
        _authoredAnchors = new AuthoredWayspotAnchorData[_authoredAnchors.Length + 1];
        oldAnchors.CopyTo(_authoredAnchors, 0);
        _authoredAnchors[oldAnchors.Length] = data;
      }

      EditorUtility.SetDirty(this);
      return data;
    }

    internal void _Remove(string manifestIdentifier)
    {
      // Maintain order of remaining anchors
      _authoredAnchors = _authoredAnchors.Where(a => a._ManifestIdentifier != manifestIdentifier).ToArray();
      _indexedAuthoredAnchors.Remove(manifestIdentifier);
      EditorUtility.SetDirty(this);
    }

    internal void _AddOrUpdateAnchorData
    (
      string manifestIdentifier,
      string anchorName = null,
      string anchorIdentifier = null,
      string payload = null,
      Vector3? position = null,
      Vector3? rotation = null,
      Vector3? scale = null,
      string tags = null,
      AuthoredWayspotAnchorData.PrefabData[] prefabs = null
    )
    {
      AuthoredWayspotAnchorData newData;
      string oldPayload = null;
      if (!_GetAnchorData(manifestIdentifier, out AuthoredWayspotAnchorData data))
      {
        newData = _AddAnchorData
        (
          anchorName,
          manifestIdentifier,
          anchorIdentifier,
          payload,
          position,
          rotation,
          scale,
          tags,
          prefabs
        );
      }
      else
      {
        oldPayload = data.Payload;

        if (prefabs != null)
        {
          var prefabsCopy = new AuthoredWayspotAnchorData.PrefabData[prefabs.Length];
          prefabsCopy = prefabs.Select(p => p.Copy()).ToArray();
          prefabs = prefabsCopy;
        }

        newData = new AuthoredWayspotAnchorData
        (
          string.IsNullOrEmpty(anchorName) ? data.Name : anchorName,
          string.IsNullOrEmpty(anchorIdentifier) ? data.Identifier : anchorIdentifier,
          string.IsNullOrEmpty(payload) ? data.Payload : payload,
          position ?? data.Position,
          rotation ?? data.Rotation,
          scale ?? data.Scale,
          string.IsNullOrEmpty(tags) ? data.Tags : tags,
          prefabs == null ? data.AssociatedPrefabs.ToArray() : prefabs,
          data._ManifestIdentifier
        );

        // Replace instead of remove/add to maintain order of remaining anchors
        var index = Array.FindIndex(_authoredAnchors, a => string.Equals(a._ManifestIdentifier, manifestIdentifier));
        _authoredAnchors[index] = newData;
        _indexedAuthoredAnchors[manifestIdentifier] = newData;
        EditorUtility.SetDirty(this);
      }
    }
  }
}
#endif
