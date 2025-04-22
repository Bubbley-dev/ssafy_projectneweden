using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public Vector2 inputVec;
    public float speed;
    public Scanner scanner;
    
    [Header("Health")]
    public float invincibleTime = 1f;
    float lastDamagedTime;
    public bool isDead = false;

    Rigidbody2D rigid;
    SpriteRenderer spriter;
    Animator anim;
    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriter = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        scanner = GetComponentInChildren<Scanner>();
    }

    void FixedUpdate()
    {
        Vector2 nextVec = speed * Time.fixedDeltaTime * inputVec;
        rigid.MovePosition(rigid.position + nextVec);
    }

    void OnMove(InputValue value)
    {
        inputVec = value.Get<Vector2>();
    }

    void LateUpdate()
    {
        anim.SetFloat("Speed", inputVec.magnitude);

        if (inputVec.x != 0)
        {
            spriter.flipX = inputVec.x < 0;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDead && collision.gameObject.CompareTag("Enemy"))
        {
            if (Time.time >= lastDamagedTime + invincibleTime)
            {
                lastDamagedTime = Time.time;
                GameManager.instance.GetDamage(10); // 데미지 양은 조절 가능
            }
        }
    }

    public void Die()
    {
        isDead = true;
        // 여기에 플레이어 사망 시 필요한 추가 처리를 구현할 수 있습니다.
    }
}
