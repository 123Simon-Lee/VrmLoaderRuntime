using SFB;
using VRM;
using System;
using UniGLTF;
using UniVRM10;
using System.IO;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using static CyanMocpapi.CyanExtensions;

public class VRMLoaderRuntime : MonoBehaviour
{
    // Start is called before the first frame update

    string migrationMessage = string.Empty;
    SynchronizationContext synchronizationContext;

    ExtensionFilter[] extensions = new[] {
        new ExtensionFilter("VRM Files", "vrm", "VRM"),
    };

    void Start()
    {
        synchronizationContext = SynchronizationContext.Current;


    }

    // Update is called once per frame
    void Update()
    {

    }

    public async void LaodVRM()
    {
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Load VRM File", "", extensions, true);

      
        await OpenVRM(paths);
    }

    private async Task OpenVRM(string[] paths)
    {
        if (paths.Length == 0) return; // ���û��ѡ���ļ�����ֱ�ӷ���
        string version=string.Empty;
                                       // �ں�̨�߳��ж�ȡ�ͽ����ļ��������������߳�
        var result = await Task.Run(() =>
        {
            // ��ȡ�ļ�����
            byte[] VRMdataRaw = File.ReadAllBytes(paths[0]);
            // ���VRM�汾
           version = DetectVRMVersion(VRMdataRaw);
            return (VRMdataRaw, version);
        });
        if (version == "VRM0")
        {

            await LoadVRM0(result.VRMdataRaw);

        }
        else if(version == "VRM1")
        {
            //VRM1 ����
            //Vrm10Instance vrm10Instance = await Vrm10.LoadPathAsync(paths[0]);
            await LoadVRM1(result.VRMdataRaw);
        }
        else
        {
            LogColor("red", "version is Empty!");
        }
}
    private string DetectVRMVersion(byte[] fileData)
    {
        
        string jsonString = System.Text.Encoding.UTF8.GetString(fileData);
        // �򵥼���Ƿ�����ض��ڰ汾�Ĺؼ���
        if (jsonString.Contains("VRMC_vrm"))
        {
            return "VRM1";
        }
        else if (jsonString.Contains("\"VRM\""))
        {
            return "VRM0";
        }
        return "Unknown";
    }

   

    private async Task LoadVRM0(byte[] VRMdataRaw)
    {
        try
        {
            GlbLowLevelParser glbLowLevelParser = new GlbLowLevelParser(null, VRMdataRaw);
            GltfData gltfData = glbLowLevelParser.Parse();
            VRMData vrm = new VRMData(gltfData);
            VRMImporterContext vrmImporter = new VRMImporterContext(vrm);

            synchronizationContext.Post(async (_) =>
            {
                RuntimeGltfInstance gltfInstance = await vrmImporter.LoadAsync(new VRMShaders.ImmediateCaller());
                gltfData.Dispose();
                vrmImporter.Dispose();
                gltfInstance.EnableUpdateWhenOffscreen();
                gltfInstance.ShowMeshes();


            }, null);
            LogColor("green", "Import VRM0 succs.");
        }
        catch (Exception e)
        {
            Debug.Log("VRM0\nLoad Error: " + e.Message);
        }

    }



    private async Task LoadVRM1(byte[] VRMdataRaw)
    {
        try
        {
            GlbLowLevelParser glbLowLevelParser = new GlbLowLevelParser(null, VRMdataRaw);
            GltfData gltfData = glbLowLevelParser.Parse();
            Vrm10Data vrm = Vrm10Data.Parse(gltfData);
            GltfData migratedGltfData = null;

            if (vrm == null)
            {
                //Auto migration
                MigrationData mdata;
                migratedGltfData = Vrm10Data.Migrate(gltfData, out vrm, out mdata);
                migrationMessage = mdata.Message;
                if (vrm == null)
                {
                    Debug.LogError(mdata.Message);
                    return;
                }
            }

            Vrm10Importer vrmImporter = new Vrm10Importer(vrm);

            synchronizationContext.Post(async (_) =>
            {
                try
                {
                    RuntimeGltfInstance gltfInstance = await vrmImporter.LoadAsync(new VRMShaders.ImmediateCaller());
                    gltfData.Dispose();
                    vrmImporter.Dispose();



                    gltfInstance.EnableUpdateWhenOffscreen();
                    gltfInstance.ShowMeshes();


                    if (migratedGltfData != null)
                    {

                        migratedGltfData.Dispose();
                    }

                }
                catch (Exception e)
                {
                    Debug.Log("VRM1\nLoad Error: " + e.Message);
                }
            }, null);
        }

        catch (Exception e)
        {
            Debug.Log("VRM1\nLoad Error: " + e.Message);
        }
        LogColor("green", "Import VRM1 succs.");
    }
}
