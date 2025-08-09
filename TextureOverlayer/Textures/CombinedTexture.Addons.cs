using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using OtterGui;
using OtterGui.Raii;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TextureOverlayer.Utils;

namespace TextureOverlayer.Textures;


//All the stuff to apply modifiers without using the native matrix code is going here 
public partial class CombinedTexture
{
    
    public Texture GetCurrent()
    {
        return _current;
    }

    public void setOps(ImageLayer layer)
    {

        _combineOp = (CombineOp)layer._combineOp;
        _resizeOp = ResizeOp.ToLeft;
    } 
    public bool SetMatrixOps(Matrix4x4 matrix)
    {
        _multiplierLeft = matrix;
        return true;
    }
    //TODO: review this https://docs.krita.org/en/reference_manual/blending_modes/arithmetic.html#subtract
    private void SubtractPixels(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = DataLeft(offset);
            var right  = DataRight(x, y);
            var alpha  = left.W;
            var rgba = alpha == 0
                           ? new Rgba32()
                           : new Rgba32(((left * left.W - right * right.W) / alpha) with { W = alpha });
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }
    //left is current layer, right is new layer also this doesnt work lol
    private void MultiplyPixels(int y, ParallelLoopState _)
    {
        GraphicsOptions options = new GraphicsOptions()
        {
            ColorBlendingMode = PixelColorBlendingMode.Multiply
        };
        
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = new Rgba32(DataLeft(offset));
            var right  = new Rgba32(DataRight(x, y));
            var rgba = new Rgba32();
            PixelBlender<Rgba32> blender = rgba.CreatePixelOperations().GetPixelBlender(options);
            rgba = blender.Blend(left, right, 0.5f);
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }
    private void SoftLight(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = DataLeft(offset);
            var right  = DataRight(x, y);
            var alpha  = left.W;
            var product = new Vector4();
            product.W = right.W;
            product.X = (float)(right.X < .5
                                    ? (2 * left.X *right.X) + (left.X * left.X * (1 - (2 * right.X))) : (2 * left.X *
                                                    (1 - right.X)) + (Math.Sqrt(left.X) * ((2 * right.X) - 1)));
            product.Y = (float)(right.Y < .5
                                    ? (2 * left.Y *right.Y ) + (left.Y * left.Y * (1 - (2 * right.Y))) : (2 * left.Y *
                                                    (1 - right.Y)) + (Math.Sqrt(left.Y) * ((2 * right.Y) - 1)));
            product.Z = (float)(right.Z < .5
                                    ? (2 * left.Z * right.Z) + (left.Z * left.Z * (1 - (2 * right.Z))) : (2 * left.Z *
                                                    (1 - right.Z)) + (Math.Sqrt(left.Z) * ((2 * right.Z) - 1)));
            
            var rgba = alpha == 0
                           ? new Rgba32()
                           : new Rgba32(((product * product.W + left * left.W * (1 - product.W)) / alpha) with { W = alpha });
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }
    
    
}
