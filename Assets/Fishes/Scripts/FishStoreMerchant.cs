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
        public GameObject template;
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
            activeProducts = new List<FishProduct>(customProducts);
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
        HashSet<string> uniqueNames = new HashSet<string>();

        foreach (FishSwim fish in sceneFishes)
        {
            // Bỏ các ký tự clone/template để có tên sạch
            string cleanName = fish.gameObject.name.Replace(" Template", "").Replace("(Clone)", "").Trim();
            if (uniqueNames.Contains(cleanName)) continue;
            uniqueNames.Add(cleanName);

            // Tạo bản sao làm template ẩn
            GameObject template = Instantiate(fish.gameObject);
            template.name = cleanName;
            template.SetActive(false);
            DontDestroyOnLoad(template);

            // Định giá dựa trên tên cá
            int price = 1000;
            if (cleanName.Contains("Rong")) price = 5000;
            else if (cleanName.Contains("Lahan") || cleanName.Contains("lahan")) price = 3000;
            else if (cleanName.Contains("Beta") || cleanName.Contains("beta")) price = 1500;
            else if (cleanName.Contains("Mau") || cleanName.Contains("mau")) price = 500;
            else if (cleanName.Contains("Cho") || cleanName.Contains("cho")) price = 800;

            activeProducts.Add(new FishProduct
            {
                name = cleanName,
                price = price,
                template = template
            });
        }
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
            GUI.Label(new Rect(winRect.x + 20f, winRect.y + 20f, winWidth - 40f, 35f), "CỬA HÀNG BÁN CÁ", titleStyle);
            
            // Vẽ Tiền tệ
            int currentMoney = PlayerMoneyDisplay.CurrentMoney;
            GUI.Label(new Rect(winRect.x + 20f, winRect.y + 55f, winWidth - 40f, 25f), $"Tiền của bạn: {PlayerMoneyDisplay.FormatVnd(currentMoney)}", labelStyle);

            // Khu vực cuộn danh sách sản phẩm
            Rect scrollArea = new Rect(winRect.x + 20f, winRect.y + 90f, winWidth - 40f, winHeight - 160f);
            Rect viewRect = new Rect(0, 0, scrollArea.width - 20f, activeProducts.Count * 70f);
            
            scrollPosition = GUI.BeginScrollView(scrollArea, scrollPosition, viewRect);
            
            for (int i = 0; i < activeProducts.Count; i++)
            {
                FishProduct prod = activeProducts[i];
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

        if (PlayerMoneyDisplay.TrySpendMoney(product.price))
        {
            // Tạo bản sao mới từ template để đưa vào túi đồ
            GameObject fishItemTemplate = Instantiate(product.template);
            fishItemTemplate.name = product.name;
            fishItemTemplate.SetActive(false);
            DontDestroyOnLoad(fishItemTemplate);

            bool success = playerInventory.AddItem(product.name, Color.white, null, fishItemTemplate);
            if (success)
            {
                Debug.Log($"Đã mua thành công {product.name}!");
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
