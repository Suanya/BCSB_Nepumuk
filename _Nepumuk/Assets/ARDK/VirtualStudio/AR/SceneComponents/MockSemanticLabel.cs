// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.ComponentModel;

using Niantic.ARDK.AR.Awareness.Semantics;
using Niantic.ARDK.Utilities.Logging;

using UnityEngine;
using UnityEngine.Assertions;

namespace Niantic.ARDK.VirtualStudio.AR.Mock
{
  /// Add this to any mesh in the Editor in order to have it semantically
  /// segmented as a certain channel.
  [RequireComponent(typeof(MeshRenderer))]
  public class MockSemanticLabel: MonoBehaviour
  {
    // Ordering of channel names here is not guaranteed to be the same as on live device.
    // The addition of new channels may cause indices to shift, so it is recommended to use
    // names rather than indices in your application code when referencing channels.
    public enum ChannelName
    {
      sky,
      ground,
      artificial_ground,
      water,
      building,
      foliage,
      grass,
      natural_ground,
      person,
      flower_experimental,
      tree_trunk_experimental,
      pet_experimental,
      sand_experimental,
      tv_experimental,
      dirt_experimental,
      vehicle_experimental,
      food_experimental,
      loungeable_experimental,
      snow_experimental
    }

    public ChannelName Channel;

    private MaterialPropertyBlock materialPropertyBlock;

    private void Awake()
    {
      var bits = ToBits(Channel);
      var color = ToColor(bits);

      materialPropertyBlock = new MaterialPropertyBlock();
      materialPropertyBlock.SetColor("PackedColor", color);
      // materialPropertyBlock.SetColor("DebugColor", _debugColors[(int)Channel]);
      GetComponent<MeshRenderer>().SetPropertyBlock(materialPropertyBlock);

      ARLog._DebugFormat("GameObject: {0} - Channel: {1} - Bits: {2}", false, gameObject.name, Channel, ToBinaryString(bits));
    }

    // Channel format copied from semantic_buffer.cpp
    static uint ToBits(ChannelName channel)
    {
      return 1u << (_NativeSemanticBuffer.BitsPerPixel - 1 - (int)channel);
    }

    static Color32 ToColor(uint buffer)
    {
      byte r = (byte)((buffer) & 0xFF);
      byte g = (byte)((buffer >> 8) & 0xFF);
      byte b = (byte)((buffer >> 16) & 0xFF);
      byte a = (byte)((buffer >> 24) & 0xFF);

      return new Color32(r, g, b, a);
    }

    public static uint ToInt(Color32 color)
    {
      uint buffer = 0;

      uint A = (uint) color.a << 24;
      uint B = (uint) color.b << 16;
      uint G = (uint) color.g << 8;
      uint R = color.r;


      buffer = R + G + B + A;

      return buffer;
    }

    public static string ToBinaryString(uint val)
    {
      var bits = Convert.ToString(val, 2).ToCharArray();
      var numBits = bits.Length;

      var a = new char[39]; // 32 bits + 7 spaces

      var ai = a.Length - 1;
      var si = numBits - 1;

      var spaceCount = 0;
      while (si >= 0)
      {
        if (spaceCount == 4)
        {
          spaceCount = 0;
          a[ai--] = ' ';
        }

        spaceCount++;

        try
        {
          a[ai--] = bits[si--];
        }
        catch (Exception e)
        {
          throw e;
        }
      }

      // Pad leading zeroes
      while (ai >= 0)
      {
        if (spaceCount == 4)
        {
          spaceCount = 0;
          a[ai--] = ' ';
        }

        spaceCount++;
        a[ai--] = '0';
      }

      return new string(a);
    }

#region DEBUG
    private static Color[] _debugColors =
    {
      Color.blue,
      new Color(165 / 255f, 42 / 255f, 42 / 255f, 1), // brown
      Color.gray,
      Color.cyan,
      Color.yellow,
      Color.red,
      Color.green,
      new Color(207 / 255f, 171 / 255f, 132 / 255f, 1), // light brown
      new Color(167 / 255f, 127 / 255f, 227 / 255f, 1), // lilac
    };
#endregion
  }
}
