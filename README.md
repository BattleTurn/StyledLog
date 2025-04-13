# Colorful Log

🎨 **Colorful Log** is a utility that enables logging messages with custom colors in the **Unity Console**. It helps you easily distinguish different log types while debugging.  

## 🚀 Features  
✅ Log messages with customizable colors using **Hex** or **RGB**  
✅ Supports `onLogEvent` for custom log formatting  
✅ Easy integration with Unity  

## 📦 Installation  

Use **Unity Package Manager** (UPM) to add the package from GitHub URL:  

```https://github.com/your-repo/colorful-log.git```

## 🔧 Usage

1️⃣ Log with Hex Color

```
using Debug = Colorful.Debug;

Debug.Log("Hello, World!", "FF0000");
``` // Logs in red

2️⃣ Log with RGB Color

```
using Debug = Colorful.Debug;

Debug.Log("Info message", new Color(0, 1, 0));
``` // Logs in green

3️⃣ Customize Log Formatting

```using Debug = Colorful.Debug;

Debug.onLogEvent += (message, parameters) => string.Format(message, parameters);

Debug.Log("Custom format!", "00FFFF");
```

4️⃣ Customize with StringBuilder

```
using Debug = Colorful.Debug;

private string AppendByStringBuilder(params object[] parameters)
{
    var sb = new StringBuilder();
    foreach (var parameter in parameters)
    {
        sb.Append(parameter);
    }
    return sb.ToString();
}
```

```Debug.onStringBuilderAppendEvent += AppendByStringBuilder;```