using UnityEngine;
using UnityEngine.UI;

public class TerribleUIManager : MonoBehaviour {

    [SerializeField]
    Button pausePlayButton;

    [SerializeField]
    Tree tree;

    bool isPause = true;
    
    public void OnPausePlayToggle() {
        isPause = !isPause;

        if (isPause) {
            pausePlayButton.GetComponentInChildren<Text>().text = "Pause";
            Time.timeScale = 1;
        }

        else {
            pausePlayButton.GetComponentInChildren<Text>().text = "Play";
            Time.timeScale = 0;
        }
    }

    public void OnResetTree() {
        tree.ResetTree();

        if (!isPause) OnPausePlayToggle();
    }

}
