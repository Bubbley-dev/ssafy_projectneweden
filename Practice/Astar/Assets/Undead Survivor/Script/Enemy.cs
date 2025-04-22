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
        coll.enabled = true;  // �浹ü Ȱ��ȭ
        rigid.simulated = true;  // ���� �ùķ��̼� Ȱ��ȭ
        spriter.sortingOrder = 2; // ��������Ʈ�� ����ִ� ������ �տ� ���̵��� ����
        anim.SetBool("Dead", false); // ���� �ִϸ��̼� �ʱ�ȭ
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
        StartCoroutine(KnockBack());  // �˹� ȿ���� ���� �ڷ�ƾ ���
        if (health > 0)
        {
            // live, Hit Action
            anim.SetTrigger("Hit");
        }
        else
        {
            // die
            isLive = false;
            coll.enabled = false;  // �浹ü ��Ȱ��ȭ
            rigid.simulated = false;  // ���� �ùķ��̼� ��Ȱ��ȭ
            spriter.sortingOrder = 1; // ��������Ʈ�� ����ִ� ������ �ڿ� ���̵��� ����
            anim.SetBool("Dead", true);
            GameManager.instance.kill++;
            GameManager.instance.GetExp();
        }
    }

    IEnumerator KnockBack()
    {
        yield return wait;  // ���� �ϳ��� ���� ������ ������
        Vector3 playerPos = GameManager.instance.player.transform.position;
        Vector3 dirVec = transform.position - playerPos;  // �÷��̾� �ݴ� ����
        rigid.AddForce(dirVec.normalized * 3, ForceMode2D.Impulse); // ��� �˹�
    }

    void Dead()
    {
        gameObject.SetActive(false);
    }
}
