using UnityEngine;

namespace MeepTech {
  public class UnityDebugger {
    public bool isEnabled = true;

    public void log(string debugMessage) {
      if (isEnabled) {
        Debug.Log(debugMessage);
      }
    }
  }
}
