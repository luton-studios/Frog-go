using UnityEngine;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

public class LutonException : System.Exception {
    public LutonException() : base() { }
    public LutonException(string message) : base(message) { }
}

public static class LutonUtils {
    public const string BUILD_VERSION = "v.0.3.0.a181212";
    public const float EPSILON = 0.000001f;

    public static readonly Vector3 LUMINOSITY_COEFFICIENTS = new Vector3(0.299f, 0.587f, 0.114f);
#if UNITY_EDITOR || DEV_BUILD
    private static readonly Rect DEBUG_DRAW_LINE_RECT = new Rect(0f, 0f, 1f, 1f);
#endif

    public static Vector2 mouseViewportPosition {
        get {
            Vector3 mousePos = Input.mousePosition;
            mousePos.x /= Screen.width;
            mousePos.y /= Screen.height;
            return mousePos;
        }
    }

    public static float mouseViewportX {
        get {
            return Input.mousePosition.x / Screen.width;
        }
    }

    public static float mouseViewportY {
        get {
            return Input.mousePosition.y / Screen.height;
        }
    }

    public static void DebugObjects(params object[] objs) {
        string s = string.Empty;

        for(int i = 0; i < objs.Length; i++) {
            s += objs[i] + "   ";
        }

        Debug.Log(s);
    }
    
    public static int GetClockSeed() {
        System.DateTime time = System.DateTime.Now;
        int seed = (int)time.Ticks;
        return seed;
    }

    private static int lastInitSeed = int.MinValue;
    public static int RandomizeUnityState() {
        int seed = GetClockSeed();

        if(seed != lastInitSeed) {
            Random.InitState(seed);
            lastInitSeed = seed;
            return seed;
        }

        return lastInitSeed;
    }
    
    public static float LerpTowards(float current, float target, float lerpSpeed, float moveTowardsSpeed, float blend) {
        if(lerpSpeed > 1f) {
            lerpSpeed = 1f;
        }
        else if(lerpSpeed < 0f) {
            lerpSpeed = 0f;
        }

        float lerpValue = Mathf.LerpUnclamped(current, target, lerpSpeed);
        float mtValue = Mathf.MoveTowards(current, target, moveTowardsSpeed);
        return Mathf.LerpUnclamped(lerpValue, mtValue, blend);
    }

    public static Vector3 LerpTowards(Vector3 current, Vector3 target, float lerpSpeed, float moveTowardsSpeed, float blend) {
        if(lerpSpeed > 1f)
            lerpSpeed = 1f;
        else if(lerpSpeed < 0f)
            lerpSpeed = 0f;

        Vector3 lerpValue = Vector3.LerpUnclamped(current, target, lerpSpeed);
        Vector3 mtValue = Vector3.MoveTowards(current, target, moveTowardsSpeed);
        return Vector3.LerpUnclamped(lerpValue, mtValue, blend);
    }

    public static void SetLayerRecursive(GameObject go, int layer) {
        go.layer = layer;
        Transform t = go.transform;

        for(int i = 0; i < t.childCount; i++) {
            Transform child = t.GetChild(i);
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    public static bool QuaternionIsNonZero(Quaternion q) {
        float sum = q.x + q.y + q.z + q.w;
        return (sum > EPSILON);
    }

    /// <summary>
    /// Clamps the value from 0 to 360. Note that it will only clamp properly if the range is from -360 to 720, it should only be used if there are minor inconsistencies.
    /// </summary>
    public static float ClampRotation(float val) {
        if(val < 0f) {
            val += 360f;
        }
        else if(val > 360f) {
            val -= 360f;
        }

        return val;
    }

    public static int Sign(float input) {
        if(input < 0f) {
            return -1;
        }
        else if(input > 0f) {
            return 1;
        }

        return 0;
    }

    public static float GetAngle(float x, float y) {
        float magnitude = (x * x) + (y * y);

        if(!Mathf.Approximately(magnitude, 1f)) {
            magnitude = Mathf.Sqrt(magnitude);
            x /= magnitude;
            x = Mathf.Clamp(x, -1f, 1f);
            y /= magnitude;
            y = Mathf.Clamp(y, -1f, 1f);
        }
        
        if(x >= 0f) {
            if(y >= 0f)
                return Mathf.Asin(x) * Mathf.Rad2Deg; // 0 to 90
            else
                return 180f - (Mathf.Asin(x) * Mathf.Rad2Deg); // 90 to 180
        }
        else {
            if(y >= 0f)
                return 360f + (Mathf.Asin(x) * Mathf.Rad2Deg); // 270 to 360
            else
                return 180f - (Mathf.Asin(x) * Mathf.Rad2Deg); // 180 to 270
        }
    }

    public static Color SetAlpha(Color col, float a) {
        col.a = a;
        return col;
    }

    public static float GetHue(this Color col) {
        float hue, saturation, value;
        Color.RGBToHSV(col, out hue, out saturation, out value);
        return hue;
    }

    public static float GetSaturation(this Color col) {
        float hue, saturation, value;
        Color.RGBToHSV(col, out hue, out saturation, out value);
        return saturation;
    }

    public static float GetValue(this Color col) {
        float hue, saturation, value;
        Color.RGBToHSV(col, out hue, out saturation, out value);
        return value;
    }

    public static float GetLuminosity(this Color col) {
        float lum = (col.r * LUMINOSITY_COEFFICIENTS.x);
        lum += (col.g * LUMINOSITY_COEFFICIENTS.y);
        lum += (col.b * LUMINOSITY_COEFFICIENTS.z);
        return lum;
    }

    public static float GetMinimumLuminance(Gradient g) {
        float min = float.MaxValue;

        foreach(GradientColorKey gck in g.colorKeys) {
            float lum = GetLuminosity(gck.color);

            if(lum < min) {
                min = lum;
            }
        }

        return min;
    }

    public static float GetMaximumLuminance(Gradient g) {
        float max = 0f;

        foreach(GradientColorKey gck in g.colorKeys) {
            float lum = GetLuminosity(gck.color);

            if(lum > max) {
                max = lum;
            }
        }

        return max;
    }
    
    public static int GetRoundedPercent(float percent) {
        if(percent > 1f)
            return Mathf.RoundToInt(percent);

        return (percent < EPSILON) ? 0 : 1; // Only 0% when it's actually 0.
    }

    public static string SanitizeString(string toSanitize, int maxLength = int.MaxValue) {
        if(string.IsNullOrEmpty(toSanitize)) {
            return toSanitize;
        }

        int length = Mathf.Min(toSanitize.Length, maxLength);
        StringBuilder sanitized = new StringBuilder(length);

        for(int i = 0; i < length; i++) {
            char c = toSanitize[i];

            // printable ASCII characters only.
            if(c >= 32 && c <= 126) {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString();
    }

    public static string RemoveSpaces(string toRemove) {
        return toRemove.Replace(" ", string.Empty);
    }

    // Displays file size as you would see in Windows Explorer (mebibytes).
    public static string GetFormattedByteSize_Windows(int bytes) {
        if(bytes >= 1073741824) // 2^30 = 1 gibibyte.
            return (bytes * 0.0000000009313225).ToString("F3") + " GiB";
        else if(bytes >= 1048576) // 2^20 = 1 mibibyte.
            return (bytes * 0.0000009536743164).ToString("F3") + " MiB";
        else if(bytes >= 1024) // 2^10 = 1 kibibyte.
            return (bytes * 0.0009765625).ToString("F3") + " KiB";
        else
            return bytes.ToString() + " B";
    }

    public static string GetFormattedTime(int seconds, bool twelveHourClock = true) {
        if(seconds < 0) {
            return "--:--";
        }

        seconds %= 86400;

        if(twelveHourClock) {
            if(seconds >= 0 && seconds < 43200) {
                int hourNum = (seconds / 3600);
                if(hourNum == 0) {
                    hourNum = 12;
                }

                return hourNum + ":" + ((seconds / 60) % 60).ToString("00") + " AM";
            }
            else {
                int hourNum = ((seconds - 43200) / 3600);
                if(hourNum == 0) {
                    hourNum = 12;
                }

                return hourNum + ":" + ((seconds / 60) % 60).ToString("00") + " PM";
            }
        }
        else {
            return (seconds / 3600).ToString("00") + ":" + ((seconds / 60) % 60).ToString("00");
        }
    }

    private static readonly System.DateTime epoch = new System.DateTime(1970, 1, 1);
    public static ulong GetUnixTimestamp() {
        double diffFromEpoch = System.DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        if(diffFromEpoch >= 0.0)
            return (ulong)diffFromEpoch;
        else
            return 0;
    }

    public static string GetTimerFormat(int seconds) {
        int minute = seconds / 60;
        int second = seconds % 60;

        if(seconds >= 3600) {
            int hour = seconds / 3600;
            minute -= (hour * 60);
            return string.Format("{0:0}:{1:00}:{2:00}", hour, minute, second);
        }
        else {
            return string.Format("{0:0}:{1:00}", minute, second);
        }
    }

    public static string GetFormattedTime(float hourOfDay, bool twelveHourClock = true) {
        return GetFormattedTime(Mathf.RoundToInt(hourOfDay * 3600f), twelveHourClock);
    }

    public static string ClampStringLength(string s, int maxLength) {
        if(!string.IsNullOrEmpty(s) && s.Length > maxLength)
            return s.Substring(0, maxLength);

        return s;
    }

    public static string StripColorBBCode(string s) {
        int openBracket = -1;
        int closeBracket = -1;
        string result = s;
        int curSearchStart = 0;

        while(!string.IsNullOrEmpty(result) && curSearchStart < result.Length) {
            openBracket = result.IndexOf('[', curSearchStart);
            closeBracket = result.IndexOf(']', curSearchStart);

            if(openBracket <= -1 || closeBracket <= -1 || closeBracket <= openBracket) {
                break;
            }

            int stripLength = (closeBracket - openBracket + 1);
            string bbCode = result.Substring(openBracket, stripLength);
            string bbCodeNoBrackets = bbCode.Substring(1, bbCode.Length - 2);
            bool isOpeningCode = bbCode != "[-]";

            if(isOpeningCode && !(bbCodeNoBrackets.Length == 6 && IsHexaDecimal(bbCodeNoBrackets))) {
                curSearchStart = closeBracket + 1;

                int endBlock = result.IndexOf("[-]", curSearchStart);
                if(endBlock > -1) {
                    curSearchStart = endBlock + 3;
                }
            }
            else {
                result = result.Remove(openBracket, stripLength);
            }
        }

        return result;
    }

    public static bool IsHexaDecimalChar(char c) {
        return ((c >= 48 && c <= 57) || (c >= 65 && c <= 70) || (c >= 97 && c <= 102)); //0-9, a-f, A-F
    }

    public static bool IsHexaDecimal(string s) {
        if(s.Length <= 0 || s.Length % 2 != 0) {
            return false;
        }

        for(int i = 0; i < s.Length; i++) {
            if(!IsHexaDecimalChar(s[i])) {
                return false;
            }
        }

        return true;
    }
    
    /// <summary>
    /// Formatted time into a string: 'MM/DD/YYYY hh:mm:ss AM/PM'
    /// </summary>
    public static string GetCurrentComputerDate() {
        System.DateTime time = System.DateTime.Now;
        return time.ToShortDateString() + " " + time.ToLongTimeString();
    }

    public static string ImprovedVector3ToString(Vector3 v, int maxDecimalPlaces = 3, bool includeSpaces = true, bool includeBrackets = true) {
        StringBuilder sb = new StringBuilder();
        float roundingThreshold = Mathf.Pow(10f, Mathf.Max(1, maxDecimalPlaces));

        if(includeBrackets) {
            sb.Append("(");
        }

        sb.Append(Mathf.Round(v.x * roundingThreshold) / roundingThreshold);
        sb.Append((includeSpaces) ? ", " : ",");

        sb.Append(Mathf.Round(v.y * roundingThreshold) / roundingThreshold);
        sb.Append((includeSpaces) ? ", " : ",");

        sb.Append(Mathf.Round(v.z * roundingThreshold) / roundingThreshold);

        if(includeBrackets) {
            sb.Append(")");
        }

        return sb.ToString();
    }

    public static int GetOccurrencesInString(string str, char toFind) {
        int count = 0;

        foreach(char c in str) {
            if(c == toFind) {
                count++;
            }
        }

        return count;
    }

    public static Vector3 ParseStringToVector3(string str, bool hasBrackets) {
        Vector3 returnedVector = Vector3.zero;

        string removedBrackets = str;
        if(hasBrackets) {
            removedBrackets = removedBrackets.Substring(1, removedBrackets.Length - 2);
        }

        removedBrackets = RemoveSpaces(removedBrackets);
        string[] splitString = removedBrackets.Split(',');

        returnedVector.x = float.Parse(splitString[0]);
        returnedVector.y = float.Parse(splitString[1]);
        returnedVector.z = float.Parse(splitString[2]);

        return returnedVector;
    }

    public static float Round(float val, int decimalPlaces = 3) {
        float roundingThreshold = 1f;

        if(decimalPlaces > 0) {
            for(int i = 0; i < decimalPlaces; i++) {
                roundingThreshold *= 10f;
            }
        }

        return Mathf.Round(val * roundingThreshold) / roundingThreshold;
    }

    public struct TerrainMeshData {
        public Vector3[] vertices;
        public int[] triangles;

        public TerrainMeshData(Vector3[] v, int[] t) {
            vertices = v;
            triangles = t;
        }
    }

    public static TerrainMeshData TerrainToMesh(Terrain ter) {
        return TerrainToMesh(ter, 0f, 1f, 0f, 1f);
    }

    public static TerrainMeshData TerrainToMesh(Terrain ter, Bounds sampleArea) {
        Transform terTrans = ter.transform;

        float relMinX = (sampleArea.min.x - terTrans.position.x) / ter.terrainData.size.x;
        float relMaxX = (sampleArea.max.x - terTrans.position.x) / ter.terrainData.size.x;
        float relMinY = (sampleArea.min.z - terTrans.position.z) / ter.terrainData.size.z;
        float relMaxY = (sampleArea.max.z - terTrans.position.z) / ter.terrainData.size.z;
        
        return TerrainToMesh(ter, relMinX, relMaxX, relMinY, relMaxY);
    }

    public static TerrainMeshData TerrainToMesh(Terrain ter, float startX, float endX, float startY, float endY) {
        Transform terTrans = ter.transform;

        startX = Mathf.Clamp(startX, 0f, endX);
        endX = Mathf.Clamp(endX, startX, 1f);
        startY = Mathf.Clamp(startY, 0f, endY);
        endY = Mathf.Clamp(endY, startY, 1f);

        int hWidth = ter.terrainData.heightmapWidth;
        int hHeight = ter.terrainData.heightmapHeight;
        Vector3 unitSize = ter.terrainData.size;
        unitSize.x /= hWidth;
        unitSize.z /= hHeight;

        int gX = Mathf.FloorToInt(startX * hWidth);
        int gY = Mathf.FloorToInt(startY * hHeight);
        int gW = Mathf.CeilToInt(endX * hWidth) - gX + 1;
        int gH = Mathf.CeilToInt(endY * hHeight) - gY + 1;

        float[,] sampledHeights = ter.terrainData.GetHeights(gX, gY, gW, gH);
        Vector3[] verts = new Vector3[gW * gH];
        int[] tris = new int[(gW - 1) * (gH - 1) * 6];

        for(int y = 0; y < gH; y++) {
            for(int x = 0; x < gW; x++) {
                verts[(y * gW) + x] = Vector3.Scale(new Vector3(gX + x, sampledHeights[y, x], gY + y), unitSize);
            }
        }

        int curIndex = 0;
        for(int y = 0; y < gH - 1; y++) {
            for(int x = 0; x < gW - 1; x++) {
                tris[curIndex++] = (y * gW) + x;
                tris[curIndex++] = ((y + 1) * gW) + x;
                tris[curIndex++] = (y * gW) + x + 1;

                tris[curIndex++] = ((y + 1) * gW) + x;
                tris[curIndex++] = ((y + 1) * gW) + x + 1;
                tris[curIndex++] = (y * gW) + x + 1;
            }
        }

        return new TerrainMeshData(verts, tris);
    }
    
    public static int GetDensestTerrainSurface(Terrain terrain, Vector3 worldPos) {
        return GetDensestTerrainSurface(terrain.transform, terrain.terrainData, worldPos);
    }
    
    public static int GetDensestTerrainSurface(Transform terrainTrans, TerrainData terrainData, Vector3 worldPos) {
        int coordX = Mathf.RoundToInt(((worldPos.x - terrainTrans.position.x) / terrainData.size.x) * terrainData.alphamapWidth);
        int coordY = Mathf.RoundToInt(((worldPos.z - terrainTrans.position.z) / terrainData.size.z) * terrainData.alphamapHeight);

        float[,,] splatmapData = terrainData.GetAlphamaps(Mathf.Clamp(coordX, 0, terrainData.alphamapWidth - 1), Mathf.Clamp(coordY, 0, terrainData.alphamapHeight - 1), 1, 1);

        float textureDensity = 0f;
        int textureIndex = -1;
        for(int i = 0; i <= splatmapData.GetUpperBound(2); i++) {
            if(splatmapData[0, 0, i] > textureDensity) {
                textureIndex = i;
                textureDensity = splatmapData[0, 0, i];
            }
        }

        return textureIndex;
    }
    
    // Simulates simple spring oscillation with dampening.
    public static void SimulateSpring(ref float current, ref float velocity, float target, float strength, float damping, int accuracy) {
        float dt = Time.deltaTime;
        float springDir = target - current;
        float dist = Mathf.Abs(springDir);
        float velocityMagn = Mathf.Abs(velocity);
        
        if(dist < 0.0001f && velocityMagn < 0.00001f)
            accuracy = 1;
        else if(dist < 0.0001f && velocityMagn < 0.001f)
            accuracy = Mathf.Max(1, accuracy / 2);

        if(accuracy > 1)
            dt /= accuracy;

        for(int i = 0; i < accuracy; i++) {
            springDir = target - current;
            velocity += (springDir * Mathf.Abs(springDir) * dt * strength);

            current += velocity * dt;
            velocity -= velocity * dt * damping;
        }
    }

    public static float GetLineSphereIntersectDistance(Vector3 lineStart, Vector3 lineEnd, Vector3 sphereCenter, float sphereRadius) {
        Vector3 dir = (lineEnd - lineStart); // Direction of line.
        float lineLength = dir.magnitude;
        dir /= lineLength; // l
        Vector3 centerToLineStart = (lineStart - sphereCenter); // o - c
        float dotResult = Vector3.Dot(dir, centerToLineStart); // l dot (o - c)
        float d = dotResult * dotResult; // d = [l dot (o - c)]^2
        d -= centerToLineStart.sqrMagnitude; // d = [l dot (o - c)]^2 - ||o - c||^2
        d += (sphereRadius * sphereRadius); // d = [l dot (o - c)]^2 - ||o - c||^2 + r^2
        d = Mathf.Sqrt(d); // d = sqrt([l dot (o - c)]^2 - ||o - c||^2 + r^2)
        d -= dotResult;

        return Mathf.Max(0f, d);
    }

    public static bool LineSphereIntersection(Vector3 lineStart, Vector3 lineEnd, Vector3 sphereCenter, float sphereRadius) {
        Vector3 dir = (lineEnd - lineStart); // Direction of line.
        float lineLengthSqr = dir.sqrMagnitude;
        float d = GetLineSphereIntersectDistance(lineStart, lineEnd, sphereCenter, sphereRadius);

        return (d * d <= lineLengthSqr);
    }

    /// <summary>
    /// Simulates projection of a unit circle onto a square.
    /// </summary>
    public static Vector2 SquareProjection(Vector2 v) {
        float magnitude = v.x*v.x + v.y*v.y;

        if(magnitude < 0.00001f) {
            return v; // Avoid division by zero and NaN errors.
        }

        magnitude = Mathf.Sqrt(magnitude);
        v /= magnitude; // Normalize into unit circle of radius 1.
        v /= Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y)); // Magic happens here: reprojection onto the edge of the overlapping 2x2 square.
        v *= magnitude; // Reapply the old magnitude.
        return v;
    }
    
    public static Vector3 Divide(Vector2 a, Vector2 b) {
        a.x /= b.x;
        a.y /= b.y;
        return a;
    }

    public static Vector3 Divide(Vector3 a, Vector3 b) {
        a.x /= b.x;
        a.y /= b.y;
        a.z /= b.z;
        return a;
    }
    
    public static Vector3 ClampVectorLength(Vector3 v, float maxLength) {
        float dist = v.magnitude;

        if(dist > maxLength)
            return (v / dist) * maxLength;

        return v;
    }

    public static T[] GetSubArray<T>(T[] sourceArray, int startIndex, int length) {
        if(length <= 0) {
            return new T[0];
        }

        T[] sub = new T[length];
        System.Array.Copy(sourceArray, startIndex, sub, 0, length);
        return sub;
    }

    public static string GetInternalIP() {
        string localIP = string.Empty;
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

        foreach(IPAddress addr in host.AddressList) {
            localIP = addr.ToString();

            string[] temp = localIP.Split('.');
            if(addr.AddressFamily == AddressFamily.InterNetwork && (temp[0] == "192" || temp[0] == "10")) {
                break;
            }
        }

        return localIP;
    }
    
    /// <summary>
    /// Get 2D perlin value from -1 to 1 instead of from 0 to 1.
    /// </summary>
    public static float PerlinNoise(float x, float y) {
        return (Mathf.PerlinNoise(x, y) - 0.5f) * 2f;
    }

    public static float SineBetween(float a, float b, float t) {
        return a + ((Mathf.Sin(t) + 1f) * 0.5f * (b - a));
    }

    public static float CosineBetween(float a, float b, float t) {
        return a + ((Mathf.Cos(t) + 1f) * 0.5f * (b - a));
    }

    public static Color RandomColor(bool includingAlpha = false) {
        if(includingAlpha)
            return new Color(Random.value, Random.value, Random.value, Random.value);
        else
            return new Color(Random.value, Random.value, Random.value);
    }

    public static Color32 RandomColor32(bool includingAlpha = false) {
        if(includingAlpha)
            return new Color32((byte)Random.Range(0, 256), (byte)Random.Range(0, 256), (byte)Random.Range(0, 256), (byte)Random.Range(0, 256));
        else
            return new Color32((byte)Random.Range(0, 256), (byte)Random.Range(0, 256), (byte)Random.Range(0, 256), 255);
    }
    
    public static string RandomCharacters(int length, bool includeUpper = false) {
        StringBuilder sb = new StringBuilder();
        int cIndex;
        RandomizeUnityState();

        for(int i = 0; i < length; i++) {
            if(includeUpper && Random.value < 0.5f) {
                cIndex = Random.Range(65, 91);
            }
            else {
                cIndex = Random.Range(97, 123);
            }
            
            sb.Append((char)cIndex);
        }

        return sb.ToString();
    }

    public static bool ContainsCharacter(string s, char c) {
        for(int i = 0; i < s.Length; i++) {
            if(s[i] == c) {
                return true;
            }
        }

        return false;
    }

    public static void DisableChildren(this GameObject go) {
        Transform trans = go.transform;
        for(int i = 0; i < trans.childCount; i++) {
            trans.GetChild(i).gameObject.SetActive(false);
        }
    }

    public static void EnableChildren(this GameObject go) {
        Transform trans = go.transform;
        for(int i = 0; i < trans.childCount; i++) {
            trans.GetChild(i).gameObject.SetActive(true);
        }
    }

    public static void Reset(this Transform t) {
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    public static void DebugDrawPoint(PrimitiveType shape, Vector3 position, float scale, Color color, float timeToLive) {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.hideFlags = HideFlags.HideAndDontSave;
        Transform t = go.transform;
        t.position = position;
        t.localScale = Vector3.one * scale;
        go.GetComponent<Collider>().enabled = false;
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.hideFlags = HideFlags.HideAndDontSave;
        mat.color = color;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        timeToLive = Mathf.Max(0.01f, timeToLive);
        Object.Destroy(go, timeToLive);
        Object.Destroy(mat, timeToLive);
    }

#if UNITY_EDITOR || DEV_BUILD
    public static void DrawLineGUI(Vector2 start, Vector2 end, float thickness, Color col) {
        if((start.x < 0f && end.x < 0f) || (start.x > Screen.width && end.x > Screen.width) || (start.y < 0f && end.y < 0f) || (start.y > Screen.height && end.y > Screen.height))
            return; // Line is out of screen.

        float dx = end.x - start.x;
        float dy = end.y - start.y;
        float length = Mathf.Sqrt(dx*dx + dy*dy);

        if(length < 0.1f)
            return; // Less than a tenth of a pixel in length. Don't bother drawing.

        float wdx = thickness * (dy / length);
        float wdy = thickness * (dx / length);

        Matrix4x4 lineMatrix = Matrix4x4.identity;
        lineMatrix.m00 = dx;
        lineMatrix.m01 = -wdx;
        lineMatrix.m03 = start.x + (wdx * 0.5f);
        lineMatrix.m10 = dy;
        lineMatrix.m11 = wdy;
        lineMatrix.m13 = start.y - (wdy * 0.5f);
        
        GL.PushMatrix();
        GL.MultMatrix(lineMatrix);

        GUI.color = col;
        GUI.DrawTexture(DEBUG_DRAW_LINE_RECT, Texture2D.whiteTexture);

        GL.PopMatrix();
    }
#endif

    public struct Conversion {
        public static readonly double STOPWATCH_FREQ_DBL = System.Diagnostics.Stopwatch.Frequency;

        public static string RGBToHex(Color32 color) {
            return color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        }

        public static Color32 HexToRGB(string hex) {
            if(hex.Length != 6) {
                return new Color32(0, 0, 0, 255);
            }

            int convertedHex = int.Parse(hex, NumberStyles.HexNumber);
            byte r = (byte)((convertedHex >> 16) & 255);
            byte g = (byte)((convertedHex >> 8) & 255);
            byte b = (byte)((convertedHex) & 255);
            return new Color32(r, g, b, 255);
        }

        public static double StopwatchTicksToSeconds(long ticks) {
            return ticks / STOPWATCH_FREQ_DBL;
        }

        public static string ArrayToString<T>(T[] arr) {
            StringBuilder sb = new StringBuilder();

            for(int i = 0; i < arr.Length; i++) {
                sb.Append(arr[i]);

                if(i < arr.Length - 1) {
                    sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        public static string ByteArrayToString(byte[] arr) {
            StringBuilder sb = new StringBuilder();

            for(int i = 0; i < arr.Length; i++) {
                sb.Append((char)arr[i]);
            }
            
            return sb.ToString();
        }

        public static byte[] StringToByteArray(string str) {
            byte[] result = new byte[str.Length];

            for(int i = 0; i < str.Length; i++) {
                result[i] = (byte)str[i];
            }

            return result;
        }

        public static float CelsiusToFahrenheit(float c) {
            return (c * 1.8f) + 32f;
        }

        public static float FahrenheitToCelsius(float f) {
            return (f - 32f) * 0.55555f;
        }
    }
    
    public struct Cryptography {
        public static readonly byte[] symmetricalKey = new byte[32] { 098, 033, 103, 101, 223, 038, 193, 188, 130, 084, 089, 049, 089, 231, 128, 096,
                                                                      114, 075, 088, 200, 255, 125, 144, 177, 166, 196, 060, 059, 190, 022, 187, 238 };

        private static AesManaged _aes;
        public static AesManaged aesInstance {
            get {
                if(_aes == null) {
                    _aes = new AesManaged();
                    _aes.Mode = CipherMode.CBC;
                    _aes.Padding = PaddingMode.ISO10126;
                    _aes.KeySize = 256;
                    _aes.BlockSize = 128;

                    _aes.Key = symmetricalKey;
                    _aes.GenerateIV();
                }

                return _aes;
            }
        }

        public static string EncryptString(string toEncrypt) {
            if(string.IsNullOrEmpty(toEncrypt)) {
                return string.Empty;
            }
            
            try {
                return System.Convert.ToBase64String(EncryptBytes(Encoding.UTF8.GetBytes(toEncrypt)));
            }
            catch(System.Exception e) {
                return e.Message;
            }
        }

        public static string DecryptString(string toDecrypt) {
            if(string.IsNullOrEmpty(toDecrypt)) {
                return string.Empty;
            }
            
            try {
                return Encoding.UTF8.GetString(DecryptBytes(System.Convert.FromBase64String(toDecrypt)));
            }
            catch(System.Exception e) {
                return e.Message;
            }
        }

        public static byte[] EncryptBytes(byte[] toEncrypt) {
            aesInstance.GenerateIV();
            ICryptoTransform enc = aesInstance.CreateEncryptor();
            byte[] data = enc.TransformFinalBlock(toEncrypt, 0, toEncrypt.Length);
            enc.Dispose();

            byte[] result = new byte[data.Length + 16];
            System.Array.Copy(aesInstance.IV, 0, result, 0, 16);
            System.Array.Copy(data, 0, result, 16, data.Length);
            return result;
        }

        public static byte[] DecryptBytes(byte[] toDecrypt) {
            const int IV_SIZE = 16;
            if(toDecrypt.Length < IV_SIZE) {
                return null;
            }

            aesInstance.IV = GetSubArray(toDecrypt, 0, IV_SIZE);
            ICryptoTransform dec = aesInstance.CreateDecryptor();
            toDecrypt = dec.TransformFinalBlock(toDecrypt, IV_SIZE, toDecrypt.Length - IV_SIZE);
            dec.Dispose();
            return toDecrypt;
        }
    }

#region Physics
    private const int MAX_ARC_RAY_COUNT = 3600;
    
    public static bool ArcCastSweep(Vector3 pos, Vector3 startDir, Vector3 endDir, float distance, int maxRayCount, out RaycastHit hit, int mask = -1, float debugLength = 0f) {
        if(distance < 0f) {
            hit = new RaycastHit();
            return false;
        }

        maxRayCount = Mathf.Clamp(maxRayCount, 1, MAX_ARC_RAY_COUNT);
        float lerpInterval = 1f / maxRayCount;

        for(float i = 0f; i < 1f; i += lerpInterval) {
            Vector3 curDir = Vector3.LerpUnclamped(startDir, endDir, i).normalized;

            RaycastHit h;
            if(Physics.Raycast(pos, curDir, out h, distance, mask)) {
                if(debugLength > 0f)
                    Debug.DrawLine(pos, h.point, Color.green, debugLength);

                hit = h;
                return true;
            }
            else if(debugLength > 0f) {
                Debug.DrawRay(pos, curDir * distance, Color.red, debugLength);
            }
        }

        hit = new RaycastHit();
        return false;
    }

    public static bool ArcCastSpread(Vector3 pos, Vector3 startDir, Vector3 endDir, float distance, int maxRayCount, out RaycastHit hit, int mask = -1, float debugLength = 0f) {
        if(distance < 0f) {
            hit = new RaycastHit();
            return false;
        }

        maxRayCount = Mathf.Clamp(maxRayCount, 1, MAX_ARC_RAY_COUNT);
        Vector3 targetDir = Vector3.LerpUnclamped(startDir, endDir, 0.5f).normalized;

        RaycastHit h;
        if(Physics.Raycast(pos, targetDir, out h, distance, mask)) {
            if(debugLength > 0f)
                Debug.DrawLine(pos, h.point, Color.green, debugLength);

            hit = h;
            return true;
        }
        else if(debugLength > 0f) {
            Debug.DrawRay(pos, targetDir * distance, Color.yellow, debugLength);
        }

        int curRay = 0;
        while(curRay < maxRayCount - 1) {
            float curSpreadFac = 2f * (((curRay / 2) + 1) / (maxRayCount - 1f));
            Vector3 curDir = Vector3.Lerp(targetDir, (curRay % 2 == 0) ? startDir : endDir, curSpreadFac).normalized;

            if(Physics.Raycast(pos, curDir, out h, distance, mask)) {
                if(debugLength > 0f)
                    Debug.DrawLine(pos, h.point, Color.green, debugLength);

                hit = h;
                return true;
            }
            else if(debugLength > 0f) {
                Debug.DrawRay(pos, curDir * distance, Color.red, debugLength);
            }

            curRay++;
        }

        hit = new RaycastHit();
        return false;
    }
    #endregion

#region Grammar
    public static string GetPossessiveFormat(string s) {
        if(!string.IsNullOrEmpty(s)) {
            if(s[s.Length - 1] == 's')
                s += "'";
            else
                s += "'s";
        }

        return s;
    }

    public static string GetPluralFormat(string nonPlural, string plural, int count) {
        if(count == 1)
            return nonPlural;

        return plural;
    }
#endregion

#if UNITY_EDITOR
    public static class Editor {
        public static void GUISeparator() {
            GUILayout.Space(2f);
            GUILayout.Box(string.Empty, GUILayout.ExpandWidth(true), GUILayout.Height(2f));
            GUILayout.Space(2f);
        }

        public static void GUISeparator(float spacing) {
            GUILayout.Space(spacing);
            GUILayout.Box(string.Empty, GUILayout.ExpandWidth(true), GUILayout.Height(2f));
            GUILayout.Space(spacing);
        }

        public static void ArrayField(UnityEditor.SerializedProperty list, string label = "") {
            UnityEditor.EditorGUILayout.PropertyField(list, new GUIContent((label.Length > 0) ? label : list.displayName));

            if(list.isExpanded) {
                UnityEditor.EditorGUI.indentLevel += 1;
                UnityEditor.EditorGUILayout.PropertyField(list.FindPropertyRelative("Array.size"));

                for(int i = 0; i < list.arraySize; i++) {
                    UnityEditor.EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(i), true);
                }

                UnityEditor.EditorGUI.indentLevel -= 1;
            }
        }

        public static void ShowRelativeProperty(UnityEditor.SerializedObject serializedObject, UnityEditor.SerializedProperty parentProperty, string propertyName) {
            UnityEditor.SerializedProperty property = parentProperty.FindPropertyRelative(propertyName);
            if(property != null) {
                UnityEditor.EditorGUI.indentLevel++;
                UnityEditor.EditorGUI.BeginChangeCheck();
                UnityEditor.EditorGUILayout.PropertyField(property, true);

                if(UnityEditor.EditorGUI.EndChangeCheck()) {
                    serializedObject.ApplyModifiedProperties();
                }

                UnityEditor.EditorGUIUtility.labelWidth = 0f;
                UnityEditor.EditorGUI.indentLevel--;
            }
        }

        public static void SetDirty(Object obj, bool markSceneAsDirty = true) {
            UnityEditor.EditorUtility.SetDirty(obj);

            if(markSceneAsDirty && UnityEditor.PrefabUtility.GetPrefabType(obj) != UnityEditor.PrefabType.Prefab && !Application.isPlaying) {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
        }
    }

    public static class GizmosEx {
        public static void DrawCross(Vector3 point, float size) {
            Gizmos.DrawLine(new Vector3(point.x - size, point.y, point.z), new Vector3(point.x + size, point.y, point.z));
            Gizmos.DrawLine(new Vector3(point.x, point.y - size, point.z), new Vector3(point.x, point.y + size, point.z));
            Gizmos.DrawLine(new Vector3(point.x, point.y, point.z - size), new Vector3(point.x, point.y, point.z + size));
        }

        public static void DrawArrow(Vector3 start, Vector3 direction, float headAngle, float headSize) {
            if(direction.sqrMagnitude < 0.0001f)
                return;

            Gizmos.DrawRay(start, direction);
            
            Vector3 arrowHeadPoint = start + direction;
            Matrix4x4 arrowMatrix = Matrix4x4.TRS(arrowHeadPoint, Quaternion.LookRotation(direction), new Vector3(headSize, headSize, headSize));
            float angleSin = Mathf.Sin(headAngle * Mathf.Deg2Rad);
            float backLength = -Mathf.Cos(headAngle * Mathf.Deg2Rad);

            Gizmos.DrawLine(arrowHeadPoint, arrowMatrix.MultiplyPoint3x4(new Vector3(-angleSin, 0f, backLength)));
            Gizmos.DrawLine(arrowHeadPoint, arrowMatrix.MultiplyPoint3x4(new Vector3(angleSin, 0f, backLength)));
            Gizmos.DrawLine(arrowHeadPoint, arrowMatrix.MultiplyPoint3x4(new Vector3(0f, -angleSin, backLength)));
            Gizmos.DrawLine(arrowHeadPoint, arrowMatrix.MultiplyPoint3x4(new Vector3(0f, angleSin, backLength)));
        }
    }
    #endif
}
