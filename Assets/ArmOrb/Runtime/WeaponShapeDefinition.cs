using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmOrb
{
    [Serializable]
    public sealed class PixelMask
    {
        public Sprite sprite;
        [Tooltip("Opaque pixel rectangles, normalised to the full texture; origin at bottom left.")]
        public Rect[] rectangles = Array.Empty<Rect>();
    }
    // Immutable geometry copied on the main thread; simulations never dereference Unity assets.
    public sealed class WeaponShapeData
    {
        public readonly PixelMask artwork, shaft, head;
        static PixelMask Copy(PixelMask mask) => new PixelMask { sprite = mask.sprite, rectangles = (Rect[])mask.rectangles.Clone() };
        public WeaponShapeData(WeaponShapeDefinition source)
        { artwork = Copy(source.artwork); shaft = Copy(source.shaft); head = Copy(source.head); }
    }
    [CreateAssetMenu(menuName = "ArmOrb/Weapon shape")]
    public sealed class WeaponShapeDefinition : ScriptableObject
    {
        public PixelMask artwork = new PixelMask();
        public PixelMask shaft = new PixelMask();
        public PixelMask head = new PixelMask();
        static readonly Dictionary<WeaponKind, WeaponShapeDefinition> cache = new Dictionary<WeaponKind, WeaponShapeDefinition>();
        public static WeaponShapeDefinition Get(WeaponKind kind)
        {
            if (cache.TryGetValue(kind, out var shape) && shape) return shape;
            shape = Resources.Load<WeaponShapeDefinition>("WeaponShapes/" + kind);
            if (!shape) throw new InvalidOperationException("Missing weapon shape: " + kind + ". Run ArmOrb/Rebuild starter scene and assets.");
            cache[kind] = shape; return shape;
        }
    }
}
