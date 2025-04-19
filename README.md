# Colorful Log

🎨 **Colorful Log** is a utility that enables logging messages with custom colors in the **Unity Console**. It helps you easily distinguish different log types while debugging.
The original **Unity Console** has a long format for coloring debugs, which requires repeating <color></color> tags when using multiple colors.

**Examples:**

***Unity Engine***
```
UnityEngine.Debug.Log("<color=#ffffff> Test Call</color> <color=#ff0000> Test Call</color> <color=#00ff00> Test Call</color>");
``` 
***Colorful Log***
```
Colorful.Debug.Log("[#ffffff: Test Call] [#ff0000: Test Call] [#00ff00: Test Call]");
```

## 🚀 Features  
✅ Log messages with customizable colors using **Hex** or **RGB**

✅ Easy integration with Unity  

## 📦 Installation  

Use **Unity Package Manager** (UPM) to add the package from GitHub URL:  

```https://github.com/your-repo/colorful-log.git```

## 🔧 Usage

1️⃣ Log with Hex Color

```
using Debug = Colorful.Debug;

Debug.Log("Hello, World!", "FF0000"); // Logs in red
```

2️⃣ Log with RGB Color

```
using Debug = Colorful.Debug;

Debug.Log("Info message", new Color(0, 1, 0)); // Logs in green
```

3️⃣ Log with multi Hex color

```
using Debug = Colorful.Debug;

Debug.LogMultiColor("[#ffffff: Test Call] [#ff0000: Test Call] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");

Debug.LogWarningMultiColor("[#ffffff: Test Call] [#ff0000: Test Call] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");

//Even using in formats.

int adaw = 1;
int adaw2 = 2;

Debug.LogMultiColor($"[#ffffff: Test Call {adaw}] [#ff0000: Test Call {adaw2}] [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]");

Debug.LogMultiColor(string.Format("[#ffffff: Test Call {0}] [#ff0000: Test Call {1}]  [#00ff00: Test Call] [#0000ff: Test Call] [#ffff00: Test Call] [#ff00ff: Test Call] [#00ffff: Test Call]", adaw, adaw2));

```