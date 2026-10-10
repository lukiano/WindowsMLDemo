# Third-party notices

## ONNX MNIST model

The bundled `Models/mnist-12.onnx` is from the ONNX Model Zoo:
https://github.com/onnx/models/tree/main/validated/vision/classification/mnist

Its model documentation specifies MIT licensing. The repository license is included in `Models/LICENSE.txt`. The trained model is used without modification.

## MNIST sample images

MNIST handwritten digit database by Yann LeCun, Corinna Cortes, and Christopher J. C. Burges:
https://yann.lecun.com/exdb/mnist/

The app includes the first 20 test images and labels, extracted from the IDX files with their headers removed and pixel values unchanged. Original download sources:

- https://storage.googleapis.com/cvdf-datasets/mnist/t10k-images-idx3-ubyte.gz
- https://storage.googleapis.com/cvdf-datasets/mnist/t10k-labels-idx1-ubyte.gz

MNIST is distributed under Creative Commons Attribution-Share Alike 3.0, as recorded in the Keras dataset documentation: https://keras.io/api/datasets/mnist/
The bundled sample subset is provided under the same terms: https://creativecommons.org/licenses/by-sa/3.0/

## Runtime dependencies

Microsoft.Windows.AI.MachineLearning and System.Numerics.Tensors are restored from NuGet. Their package metadata, notices, and licenses apply to the runtime binaries. .NET runtime notices are included by self-contained publishing.

TensorRT for RTX is acquired separately through Windows ML's execution-provider catalog rather than bundled in this repository. NVIDIA's applicable license terms govern that provider: https://docs.nvidia.com/deeplearning/tensorrt-rtx/latest/reference/license.html
