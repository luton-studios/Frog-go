using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(AchievementController))]
public class AchievementControllerEditor : Editor {
    private AchievementID idToMove;
    private AchievementID placeBelow;
    
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        AchievementController ac = (AchievementController)target;

        GUILayout.Space(10f);

        idToMove = (AchievementID)EditorGUILayout.EnumPopup("ID To Move:", idToMove);
        placeBelow = (AchievementID)EditorGUILayout.EnumPopup("Place Below:", placeBelow);

        if(GUILayout.Button("Move Achievement")) {
            if(idToMove != placeBelow) {
                int moveAchvIndex = -1;
                int placeBelowIndex = -1;

                for(int i = 0; i < ac.allAchievements.Length; i++) {
                    if(ac.allAchievements[i].id == idToMove) {
                        moveAchvIndex = i;
                        break;
                    }
                }

                if(moveAchvIndex >= 0) {
                    for(int i = 0; i < ac.allAchievements.Length; i++) {
                        if(ac.allAchievements[i].id == placeBelow) {
                            placeBelowIndex = i;
                            break;
                        }
                    }

                    int targetIndex = placeBelowIndex + 1;

                    // Check if we are already below this target ID.
                    if(placeBelowIndex > -1 && moveAchvIndex != targetIndex) {
                        List<Achievement> achvList = new List<Achievement>(ac.allAchievements);
                        bool movedUp = false;

                        if(moveAchvIndex > ac.allAchievements.Length - 1) {
                            achvList.Add(ac.allAchievements[moveAchvIndex]); // Place at the end.
                        }
                        else {
                            achvList.Insert(targetIndex, achvList[moveAchvIndex]);
                            movedUp = (targetIndex < moveAchvIndex);
                        }

                        if(movedUp)
                            achvList.RemoveAt(moveAchvIndex + 1);
                        else
                            achvList.RemoveAt(moveAchvIndex);

                        // Apply array changes.
                        ac.allAchievements = achvList.ToArray();
                        LutonUtils.Editor.SetDirty(ac);
                    }
                }
            }
        }
    }
}