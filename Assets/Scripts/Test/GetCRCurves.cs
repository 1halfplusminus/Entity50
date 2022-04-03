using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;

public class GetCRCurves : MonoBehaviour {

    void Start() {
        var animationClips = AnimationUtility.GetAnimationClips(gameObject);
        for (int i = 0 ; i < animationClips.Length; i++){
            var animationClip = animationClips[i];
            var curveBindings = AnimationUtility.GetCurveBindings(animationClip);
            for (var j = 0; j <  curveBindings.Length; j++) {
                var curveBinding = curveBindings[j];
                if(curveBinding.propertyName == "localEulerAnglesRaw.y"){
                    // Debug.Log ($"{curveBinding.path}.{curveBinding.propertyName}");
                    // var animationCurve =  AnimationUtility.GetEditorCurve (animationClip, curveBinding);
                    // curveBinding.path = "";
                    // curveBinding.propertyName = "test";   
                    // curveBinding.type = typeof(PropertyStreamHandleExample);
                    // AnimationUtility.SetEditorCurve(animationClip,curveBinding,animationCurve);
                }
                
            }
        }
    }
}