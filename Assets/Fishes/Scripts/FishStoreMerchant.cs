using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class FishStoreMerchant : MonoBehaviour
{
    [System.Serializable]
    public struct FishProduct
    {
        public string name;
        public int price;
        public GameObject template; // Dành cho cấu hình đơn lẻ trong Inspector
        [HideInInspector]
        public List<GameObject> templates; // Danh sách các bản mẫu kích thước khác nhau ở runtime
        public string group; // Nhóm loài cá (Ví dụ: Cá Cảnh, Cá Rồng...)
    }

    [Header("Cài đặt tương tác")]
    public float interactionDistance = 3.5f;
    public string openPrompt = "Nhấn F để mở Cửa hàng Cá";
    
    [Header("Danh sách Cá bán (Để trống sẽ tự động lấy cá trong bể)")]
    public List<FishProduct> customProducts = new List<FishProduct>();

    public static bool IsOpen { get; private set; }

    private Transform player;
    private SimpleInventory playerInventory;
    private MinhThirdPersonController playerController;
    private List<FishProduct> activeProducts = new List<FishProduct>();
    private List<string> categories = new List<string>();
    private int selectedCategoryIndex = 0;
    private bool isPlayerNear;
    
    private GUIStyle promptStyle;
    private GUIStyle windowStyle;
    private GUIStyle buttonStyle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private Vector2 scrollPosition;

    private void Start()
    {
        FindPlayerReferences();

        // Tự động tìm cá trong bể làm mẫu nếu không có custom products
        if (customProducts.Count == 0)
        {
            InitializeDefaultProducts();
        }
        else
        {
            activeProducts = new List<FishProduct>();
            foreach (var prod in customProducts)
            {
                FishProduct sanitized = prod;
                if (string.IsNullOrEmpty(sanitized.group))
                {
                    sanitized.group = "Cá Cảnh";
                }
                
                // Đảm bảo list templates được khởi tạo từ template đơn lẻ của Inspector
                if (sanitized.templates == null || sanitized.templates.Count == 0)
                {
                    sanitized.templates = new List<GameObject>();
                    if (prod.template != null)
                    {
                        sanitized.templates.Add(prod.template);
                    }
                }
                activeProducts.Add(sanitized);
            }
        }

        // Tạo danh mục nhóm cá độc nhất
        categories.Clear();
        categories.Add("Tất Cả");
        foreach (var prod in activeProducts)
        {
            if (!string.IsNullOrEmpty(prod.group) && !categories.Contains(prod.group))
            {
                categories.Add(prod.group);
            }
        }
    }

    private void FindPlayerReferences()
    {
        MinhThirdPersonController controller = FindFirstObjectByType<MinhThirdPersonController>();
        if (controller != null)
        {
            player = controller.transform;
            playerController = controller;
            playerInventory = controller.GetComponent<SimpleInventory>();
        }
    }

    private void InitializeDefaultProducts()
    {
        FishSwim[] sceneFishes = FindObjectsByType<FishSwim>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        // Dictionary để gom nhóm các con cá cùng loài (có kích thước scale khác nhau)
        Dictionary<string, FishProduct> productDict = new Dictionary<string, FishProduct>();

        foreach (FishSwim fish in sceneFishes)
        {
            // Bỏ các ký tự clone/template và cả số thứ tự tự động của Unity (ví dụ " 1", "(2)", etc.)
            string cleanName = fish.gameObject.name.Replace(" Template", "").Replace("(Clone)", "").Trim();
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s*\(?\d+\)?$", "").Trim();

            // Tạo bản sao làm template ẩn (Lưu giữ nguyên tỉ lệ kích thước thật của con cá này trong scene)
            GameObject template = Instantiate(fish.gameObject);
            template.name = cleanName;
            template.SetActive(false);
            DontDestroyOnLoad(template);

            if (productDict.ContainsKey(cleanName))
            {
                // Nếu loài cá này đã có, thêm bản mẫu kích thước này vào danh sách
                productDict[cleanName].templates.Add(template);
            }
            else
            {
                // Định giá dựa trên tên cá
                int price = 1000;
                if (cleanName.Contains("Rong")) price = 5000;
                else if (cleanName.Contains("Lahan") || cleanName.Contains("lahan")) price = 3000;
                else if (cleanName.Contains("Beta") || cleanName.Contains("beta")) price = 1500;
                else if (cleanName.Contains("Mau") || cleanName.Contains("mau")) price = 500;
                else if (cleanName.Contains("Cho") || cleanName.Contains("cho")) price = 800;

                // Lấy nhóm của cá
                string group = string.IsNullOrEmpty(fish.fishGroup) ? "Cá Cảnh" : fish.fishGroup;

                FishProduct newProduct = new FishProduct
                {
                    name = cleanName,
                    price = price,
                    templates = new List<GameObject> { template },
                    group = group
                };
                productDict[cleanName] = newProduct;
            }
        }

        // Đưa các sản phẩm đã gom nhóm vào activeProducts
        activeProducts = new List<FishProduct>(productDict.Values);
    }

    private void Update()
    {
        if (player == null || playerInventory == null || playerController == null)
        {
            FindPlayerReferences();
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        isPlayerNear = distance <= interactionDistance;

        if (!isPlayerNear && IsOpen)
        {
            CloseStore();
        }

        if (isPlayerNear && !PauseMenuManager.GameIsPaused)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    CloseStore();
                }
                else
                {
                    OpenStore();
                }
            }
        }
    }

    private void OpenStore()
    {
        IsOpen = true;
        
        // Khóa di chuyển nhân vật
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // Mở khóa chuột
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseStore()
    {
        IsOpen = false;

        // Bật lại di chuyển nhân vật
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // Khóa lại chuột
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        // Đảm bảo không bị kẹt khóa player nếu script bị tắt đột ngột
        if (IsOpen)
        {
            CloseStore();
        }
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused) return;

        EnsureStyles();

        if (isPlayerNear && !IsOpen)
        {
            float width = 300f;
            float height = 42f;
            Rect rect = new((Screen.width - width) * 0.5f, Screen.height - 160f, width, height);
            GUI.Box(rect, openPrompt, promptStyle);
        }

        if (IsOpen)
        {
            float winWidth = 450f;
            float winHeight = 500f;
            Rect winRect = new((Screen.width - winWidth) * 0.5f, (Screen.height - winHeight) * 0.5f, winWidth, winHeight);
            
            GUI.Box(winRect, "", windowStyle);
            
            // Vẽ Tiêu đề
            GUI.Label(new Rect(winRect.x + 20f, winRect.y + 15f, winWidth - 40f, 30f), "CỬA HÀNG BÁN CÁ", titleStyle);
            
            // Vẽ Tiền tệ
            int currentMoney = PlayerMoneyDisplay.CurrentMoney;
            GUI.Label(new Rect(winRect.x + 20f, winRect.y + 45f, winWidth - 40f, 20f), $"Tiền của bạn: {PlayerMoneyDisplay.FormatVnd(currentMoney)}", labelStyle);

            // Vẽ hàng Tab phân loại nhóm cá
            float totalWidth = winWidth - 40f;
            float tabGap = 4f;
            float tabWidth = (totalWidth - (categories.Count - 1) * tabGap) / categories.Count;
            float startTabX = winRect.x + 20f;
            float tabY = winRect.y + 70f;
            
            for (int c = 0; c < categories.Count; c++)
            {
                Rect tabRect = new Rect(startTabX + c * (tabWidth + tabGap), tabY, tabWidth, 28f);
                
                GUIStyle tabStyle = new GUIStyle(GUI.skin.button);
                if (c == selectedCategoryIndex)
                {
                    tabStyle.normal.textColor = Color.cyan;
                    tabStyle.fontStyle = FontStyle.Bold;
                }
                
                if (GUI.Button(tabRect, categories[c], tabStyle))
                {
                    selectedCategoryIndex = c;
                    scrollPosition = Vector2.zero;
                }
            }

            // Lọc danh sách cá theo nhóm được chọn
            List<FishProduct> displayProducts = new List<FishProduct>();
            string currentCategory = categories[selectedCategoryIndex];
            foreach (var prod in activeProducts)
            {
                if (currentCategory == "Tất Cả" || prod.group == currentCategory)
                {
                    displayProducts.Add(prod);
                }
            }

            // Khu vực cuộn danh sách sản phẩm
            Rect scrollArea = new Rect(winRect.x + 20f, winRect.y + 110f, winWidth - 40f, winHeight - 175f);
            Rect viewRect = new Rect(0, 0, scrollArea.width - 20f, displayProducts.Count * 70f);
            
            scrollPosition = GUI.BeginScrollView(scrollArea, scrollPosition, viewRect);
            
            for (int i = 0; i < displayProducts.Count; i++)
            {
                FishProduct prod = displayProducts[i];
                float itemY = i * 70f;
                
                // Khung sản phẩm
                GUI.Box(new Rect(0, itemY, viewRect.width, 60f), "");
                
                // Tên và giá
                GUI.Label(new Rect(15f, itemY + 10f, 220f, 20f), prod.name, labelStyle);
                GUI.Label(new Rect(15f, itemY + 30f, 220f, 20f), $"Giá: {PlayerMoneyDisplay.FormatVnd(prod.price)}", labelStyle);
                
                // Nút Mua
                if (GUI.Button(new Rect(viewRect.width - 110f, itemY + 12f, 100f, 36f), "Mua", buttonStyle))
                {
                    BuyFish(prod);
                }
            }
            
            GUI.EndScrollView();
            
            // Nút Đóng
            if (GUI.Button(new Rect(winRect.x + (winWidth - 120f) * 0.5f, winRect.y + winHeight - 55f, 120f, 40f), "ĐÓNG (F)", buttonStyle))
            {
                CloseStore();
            }
        }
    }

    private void BuyFish(FishProduct product)
    {
        if (playerInventory == null) return;

        if (playerInventory.IsFull())
        {
            Debug.Log("Hành trang của bạn đã đầy!");
            return;
        }

        if (!PlayerMoneyDisplay.CanAfford(product.price))
        {
            Debug.Log("Bạn không đủ tiền mua cá!");
            return;
        }

        if (product.templates == null || product.templates.Count == 0)
        {
            Debug.LogWarning("Không có bản mẫu cá nào để mua!");
            return;
        }

        if (PlayerMoneyDisplay.TrySpendMoney(product.price))
        {
            // Chọn ngẫu nhiên 1 kích thước/bản mẫu trong danh sách các con cá cùng loài tìm thấy trong bể
            int randomIndex = Random.Range(0, product.templates.Count);
            GameObject chosenTemplate = product.templates[randomIndex];

            // Tạo bản sao mới từ template được chọn để đưa vào túi đồ (đảm bảo giữ nguyên scale kích thước của con đó)
            GameObject fishItemTemplate = Instantiate(chosenTemplate);
            fishItemTemplate.name = product.name;
            fishItemTemplate.SetActive(false);
            DontDestroyOnLoad(fishItemTemplate);

            bool success = playerInventory.AddItem(product.name, Color.white, null, fishItemTemplate);
            if (success)
            {
                Debug.Log($"Đã mua thành công {product.name} (Chọn ngẫu nhiên mẫu kích thước #{randomIndex})!");
            }
            else
            {
                // Trả lại tiền nếu có lỗi xảy ra
                PlayerMoneyDisplay.AddMoney(product.price);
                Destroy(fishItemTemplate);
            }
        }
    }

    private void EnsureStyles()
    {
        if (promptStyle != null) return;

        promptStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        windowStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.16f, 0.96f)) }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.cyan }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
