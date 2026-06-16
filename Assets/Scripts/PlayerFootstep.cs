using UnityEngine;

public class PlayerFootstep : MonoBehaviour
{
    [Header("Cài đặt chung")]
    public AudioSource audioSource;

    [Header("Âm thanh các loại mặt đất")]
    public AudioClip[] woodClips;
    public AudioClip[] grassClips;
    public AudioClip[] stoneClips;
    public AudioClip[] defaultClips; // Dùng khi không nhận diện được mặt đất

    // Hàm này SẼ KHÔNG gọi trong Update(), mà sẽ được gọi bởi Hoạt ảnh (Animation)
    public void PlayFootstepSound()
    {
        // Bắn một tia Raycast từ vị trí nhân vật (cộng thêm 0.5m lên trên để tia bắt đầu từ khoảng đầu gối/bụng) cắm thẳng xuống đất
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1f))
        {
            AudioClip[] selectedClips = defaultClips;

            // Dò xem mặt đất đang có Tag gì
            switch (hit.collider.tag)
            {
                case "Wood": selectedClips = woodClips; break;
                case "Grass": selectedClips = grassClips; break;
                case "Stone": selectedClips = stoneClips; break;
            }

            // Nếu mảng âm thanh có chứa dữ liệu thì phát ngẫu nhiên 1 âm thanh
            if (selectedClips.Length > 0)
            {
                int randomIndex = Random.Range(0, selectedClips.Length);

                // MẸO: Thay đổi cao độ (Pitch) một chút xíu để mỗi bước chân nghe hơi khác nhau, tạo sự chân thực
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.volume = Random.Range(0.8f, 1f);

                // Phát âm thanh
                audioSource.PlayOneShot(selectedClips[randomIndex]);
            }
        }
    }
}