using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build;
#endif

#if UNITY_EDITOR
public static class DetectRenderPipeline
{

	public static readonly string[] usingRPSymbols = new string[] {
			 "USING_HDRP",
			 "USING_URP",
			 "USING_BUILTIN",
		 };

	static string CleanPreprocessorDirectives(string definedSymbolsString)
	{
		List<string> allDefinedSymbols = definedSymbolsString.Split(';').ToList();

		foreach (string removeSymbol in usingRPSymbols)
		{
			allDefinedSymbols.Remove(removeSymbol);
		}

		// Also remove legacy USING_2019 symbol if present
		allDefinedSymbols.Remove("USING_2019");

		return string.Join(";", allDefinedSymbols.ToArray());
	}

	static string DetectAndSetSymbolString(string definedSymbols)
	{
		string newSymbolString = "";

		if (definedSymbols != "")
		{
			newSymbolString = definedSymbols + ";";
		}
		if (Daz3D.RenderPipelineHelper.IsHDRP)
		{
			newSymbolString += "USING_HDRP";
		}
		else if (Daz3D.RenderPipelineHelper.IsURP)
		{
			newSymbolString += "USING_URP";
		}
		else
		{
			newSymbolString += "USING_BUILTIN";
		}

		return newSymbolString;
	}

	static void CommitDefinedSymbols(string newSymbolsString)
	{
		Debug.Log("Attempting to write new PreprocessorDirectives: " + newSymbolsString);
		var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_6000_0_OR_NEWER
		PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup), newSymbolsString);
#else
		PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newSymbolsString);
#endif
	}

	static string GetCurrentDefinedSymbols()
	{
		var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_6000_0_OR_NEWER
		PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup), out string[] defines);
		return string.Join(";", defines);
#else
		return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
	}

	public static void RunOnce()
	{
		Daz3D.RenderPipelineHelper.InvalidateCache();

		string oldSymbolsString = GetCurrentDefinedSymbols();

		string newSymbolsString = oldSymbolsString;

		newSymbolsString = CleanPreprocessorDirectives(newSymbolsString);
		newSymbolsString = DetectAndSetSymbolString(newSymbolsString);

		string renderPipelineString = "Built-In Pipeline";
		if (Daz3D.RenderPipelineHelper.IsHDRP)
			renderPipelineString = "HDRP";
		else if (Daz3D.RenderPipelineHelper.IsURP)
			renderPipelineString = "URP";

		if (oldSymbolsString != newSymbolsString)
		{
			Daz3D.Daz3DBridge.CurrentToolbarMode = Daz3D.Daz3DBridge.ToolbarMode.Options;
			string dtu_detectrp_message = "Detected [" + renderPipelineString + "]\n\nDTU Bridge must update symbol definitions to continue the Import Procedure.  This may take a few minutes.  " +
				"You may Cancel now, and rerun the renderpipeline detection process from the DTU Bridge at another time.";
			bool bUpdateSymbols = EditorUtility.DisplayDialog("RenderPipeline Detection", dtu_detectrp_message, "Update Symbol Definitions Now", "Cancel and Redetect RenderPipeline Later");
			if (bUpdateSymbols)
			{
				Daz3D.Daz3DBridge.CurrentToolbarMode = Daz3D.Daz3DBridge.ToolbarMode.History;
				CommitDefinedSymbols(newSymbolsString);
				Daz3D.Daz3DDTUImporter.ImportEventRecord record = new Daz3D.Daz3DDTUImporter.ImportEventRecord();
				Daz3D.Daz3DDTUImporter.EventQueue.Enqueue(record);
				record.AddToken("Updating Symbol Definitions.\nThis will trigger Unity Editor to recompile all scripts and may take several minutes...");
			}
			else
			{
				Daz3D.Daz3DDTUImporter.ImportEventRecord record = new Daz3D.Daz3DDTUImporter.ImportEventRecord();
				Daz3D.Daz3DDTUImporter.EventQueue.Enqueue(record);
				record.AddToken("Autodetection cancelled.\nPlease click the \"Detect RenderPipeline\" button \nin the Options tab to continue import procedure.");
			}

		}
		else
		{
			string dtu_detectrp_message = "Detected [" + renderPipelineString + "]\n\nNo changes need to be made to Symbol Definitions.";
			EditorUtility.DisplayDialog("RenderPipeline Detection", dtu_detectrp_message, "OK");
		}

	}
}
#endif // UNITY_EDITOR
