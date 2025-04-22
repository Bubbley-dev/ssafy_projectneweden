using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed;
    public float health;
    public float maxHealth;
    public RuntimeAnimatorController[] animCon;
    public Rigidbody2D target;

    bool isLive;
    Rigidbody2D rigid;
    Collider2D coll;
    Animator anim;
    SpriteRenderer spriter;
    WaitForFixedUpdate wait;

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        spriter = GetComponent<SpriteRenderer>();
        wait = new WaitForFixedUpdate();
    }

    void FixedUpdate()
    {
        if (!isLive || anim.GetCurrentAnimatorStateInfo(0).IsName("Hit")) return;

        Vector2 dirVec = target.position - rigid.position;
        Vector2 nextVect = speed * Time.fixedDeltaTime * dirVec.normalized;
        rigid.MovePosition(rigid.position + nextVect);
        rigid.linearVelocity = Vector2.zero;
    }

    void LateUpdate()
    {
        if (!isLive) return;
        spriter.flipX = target.position.x < rigid.position.x;
    }

    private void OnEnable()
    {
        target = GameManager.instance.player.GetComponent<Rigidbody2D>();
        isLive = true;
        coll.enabled = true;  // 충돌체 활성화
        rigid.simulated = true;  // 물리 시뮬레이션 활성화
        spriter.sortingOrder = 2; // 스프라이트가 살아있는 적보다 앞에 보이도록 설정
        anim.SetBool("Dead", false); // 죽음 애니메이션 초기화
        health = maxHealth;
    }

    public void Init(SpawnData data)
    {
        anim.runtimeAnimatorController = animCon[data.spriteType];
        speed = data.speed;
        maxHealth = data.health;
        health = data.health;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Bullet") || !isLive) return;

        health -= collision.GetComponent<Bullet>().damage;
        StartCoroutine(KnockBack());  // 넉백 효과를 위해 코루틴 사용
        if (health > 0)
        {
            // live, Hit Action
            anim.SetTrigger("Hit");
        }
        else
        {
            // die
            isLive = false;
            coll.enabled = false;  // 충돌체 비활성화
            rigid.simulated = false;  // 물리 시뮬레이션 비활성화
            spriter.sortingOrder = 1; // 스프라이트가 살아있는 적보다 뒤에 보이도록 설정
            anim.SetBool("Dead", true);
            GameManager.instance.kill++;
            GameManager.instance.GetExp();
        }
    }

    IEnumerator KnockBack()
    {
        yield return wait;  // 다음 하나의 물리 프레임 딜레이
        Vector3 playerPos = GameManager.instance.player.transform.position;
        Vector3 dirVec = transform.position - playerPos;  // 플레이어 반대 방향
        rigid.AddForce(dirVec.normalized * 3, ForceMode2D.Impulse); // 즉시 넉백
    }

    void Dead()
    {
        gameObject.SetActive(false);
    }
}
