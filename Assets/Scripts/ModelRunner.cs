using UnityEngine;
using Unity.Barracuda;

public class ModelRunner : MonoBehaviour
{
    [Header("Model Settings")]
    public NNModel modelAsset;         // Drag your ONNX model here from the Assets folder.
    public string inputName = "arg_0"; // Update based on your model's input name.
    public string outputName = "conv2d_18"; // Update based on your model's output name.

    private Model runtimeModel;
    private IWorker worker;

    void Start()
    {
        if (modelAsset == null)
        {
            Debug.LogError("Model asset is not assigned!");
            return;
        }

        // Load the ONNX model
        runtimeModel = ModelLoader.Load(modelAsset);
        if (runtimeModel == null)
        {
            Debug.LogError("Failed to load the model!");
            return;
        }

        Debug.Log("Model loaded successfully: " + runtimeModel);
        
        // Create a worker to run inference (Auto chooses the best backend available)
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
    }

    /// <summary>
    /// Run inference on a given input texture and return the output tensor.
    /// </summary>
    /// <param name="inputTexture">The input image as a Texture2D</param>
    /// <returns>Output tensor from the model</returns>
    public Tensor RunInference(Texture2D inputTexture)
    {
        if (worker == null || inputTexture == null)
        {
            Debug.LogError("Worker not initialized or input texture is null!");
            return null;
        }

        // Convert the Texture2D to a Tensor.
        Tensor inputTensor = new Tensor(inputTexture, channels: 3);

        // Execute inference on the tensor.
        worker.Execute(inputTensor);

        // Get the output tensor by name.
        Tensor outputTensor = worker.PeekOutput(outputName);

        // Dispose the input tensor after use.
        inputTensor.Dispose();

        return outputTensor;
    }

    void OnDisable()
    {
        if (worker != null)
        {
            worker.Dispose();
            worker = null;
        }
    }

    // For demonstration: Visualize model output on a texture.
    void OnGUI()
    {
        Texture2D sampleTexture = Resources.Load<Texture2D>("sample"); // Ensure "sample.png" exists in Assets/Resources/
        if (sampleTexture != null)
        {
            Tensor output = RunInference(sampleTexture);
            if (output != null)
            {
                // Process the output tensor to a Texture2D (assume single-channel output for segmentation)
                Texture2D outputTexture = new Texture2D(output.shape.width, output.shape.height, TextureFormat.RFloat, false);
                float[] outputData = output.ToReadOnlyArray();
                Color[] pixels = new Color[outputData.Length];
                for (int i = 0; i < outputData.Length; i++)
                {
                    // For binary segmentation, use the value as grayscale.
                    pixels[i] = new Color(outputData[i], outputData[i], outputData[i]);
                }
                outputTexture.SetPixels(pixels);
                outputTexture.Apply();

                // Display the sample and output textures
                GUI.DrawTexture(new Rect(10, 10, 256, 256), sampleTexture, ScaleMode.ScaleToFit);
                GUI.DrawTexture(new Rect(276, 10, 256, 256), outputTexture, ScaleMode.ScaleToFit);
            }
            output?.Dispose();
        }
        else
        {
            GUI.Label(new Rect(10, 10, 300, 20), "Place a sample image in Assets/Resources/sample.png");
        }
    }
}
