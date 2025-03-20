# Colorful Log

🎨 **Colorful Log** is a utility that enables logging messages with custom colors in the **Unity Console**. It helps you easily distinguish different log types while debugging.  

## 🚀 Features  
✅ Log messages with customizable colors using **Hex** or **RGB**  
✅ Supports `onLogEvent` for custom log formatting  
✅ Easy integration with Unity  

## 📦 Installation  

Use **Unity Package Manager** (UPM) to add the package from GitHub:  

```json
{
  "dependencies": {
    "com.yourname.colorful-log": "https://github.com/your-repo/colorful-log.git"
  }
}

## 🔧 Usage
1️⃣ Log with Hex Color
```LogColorful.Log("Hello, World!", "FF0000", Debug.Log); // Logs in red
2️⃣ Log with RGB Color
```LogColorful.Log("Info message", new Color(0, 1, 0), Debug.Log); // Logs in green
3️⃣ Customize Log Formatting
```LogColorful.onLogEvent += (message, color) => $"[LOG] <color=#{color}>{message}</color>";
```LogColorful.Log("Custom format!", "00FFFF", Debug.Log);