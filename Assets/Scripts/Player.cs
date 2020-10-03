using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int id;
    public string userName;

    public CharacterController controller;
    public float gravity = -9.81f;
    public float moveSpeed = 5f;
    public float jumpSpeed = 5f;
    public Transform ShootOrigin;
    public float health;
    public float maxHealth = 100f;

    private bool[] _inputs;
    private float _yVelocity = 0;

    private void Start()
    {
        gravity *= Time.fixedDeltaTime * Time.fixedDeltaTime;
        moveSpeed *= Time.fixedDeltaTime;
        jumpSpeed *= Time.fixedDeltaTime;
    }

    public void Initialize(int id, string userName)
    {
        this.id = id;
        this.userName = userName;
        health = maxHealth;
        _inputs = new bool[5];
    }

    public void FixedUpdate()
    {
        if (health <= 0f)
        {
            return;
        }

        Vector2 inputDirection = Vector2.zero;
        if (_inputs[0])
        {
            inputDirection.y += 1;
        }

        if (_inputs[1])
        {
            inputDirection.y -= 1;
        }

        if (_inputs[2])
        {
            inputDirection.x -= 1;
        }

        if (_inputs[3])
        {
            inputDirection.x += 1;
        }

        Move(inputDirection);
    }

    private void Move(Vector2 inputDirection)
    {
        Vector3 moveDirection = transform.right * inputDirection.x + transform.forward * inputDirection.y;
        moveDirection *= moveSpeed;

        if (controller.isGrounded)
        {
            _yVelocity = 0f;
            if (_inputs[4])
            {
                _yVelocity = jumpSpeed;
            }
        }

        _yVelocity += gravity;
        moveDirection.y = _yVelocity;

        controller.Move(moveDirection);

        ServerSend.PlayerPosition(this);
        ServerSend.PlayerRotation(this);
    }

    public void SetInputs(bool[] inputs, Quaternion rotation)
    {
        _inputs = inputs;
        transform.rotation = rotation;
    }

    public void Shoot(Vector3 viewDirection)
    {
        if (Physics.Raycast(ShootOrigin.position, viewDirection, out RaycastHit hit, 25f))
        {
            if (hit.collider.CompareTag("Player"))
            {
                hit.collider.GetComponent<Player>().TakeDamage(50f);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (health <= 0)
        {
            return;
        }

        health -= damage;
        if (health <= 0)
        {
            health = 0f;
            controller.enabled = false;
            transform.position = new Vector3(0f, 25f, 0f);
            ServerSend.PlayerPosition(this);
            StartCoroutine(Respawn());
        }
        
        ServerSend.PlayerHealth(this);
    }

    public IEnumerator Respawn()
    {
        yield return new WaitForSeconds(5f);
        health = maxHealth;
        controller.enabled = true;
        ServerSend.PlayerRespawned(this);
    }
}