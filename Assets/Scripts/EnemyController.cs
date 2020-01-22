﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour {
    private UnityEngine.AI.NavMeshAgent agent;
    public GameObject player;
    public State state;
    public enum State {
        IDLE,
        CHASE,
        ATTACK
    }
    private Animator animator;
    public Weapon weapon;
    public float hitSpeed = 1f;
    // Start is called before the first frame update
    void Start() {
        agent = gameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
        state = State.CHASE;
        animator = gameObject.GetComponent<Animator>();
        animator.SetFloat("hitSpeed", hitSpeed);
        weapon.AttackDuration = weapon.AttackDuration / hitSpeed;
    }

    // Update is called once per frame
    void Update() {
        switch (state) {
            case State.CHASE:
                Chase();
                break;
            case State.IDLE:
                break;
            case State.ATTACK:
                Attack();
                break;
        }
    }

    private void Chase() {
        if (Vector3.Distance(player.transform.position, transform.position) < 5) {

            agent.isStopped = true;
            state = State.ATTACK;
            return;
        }
        agent.isStopped = false;
        agent.destination = player.transform.position;
        animator.SetBool("isWalking", true);
    }

    private void Attack() {
        weapon.Attack();
        animator.SetBool("isAttacking", true);
        state = State.IDLE;
        StartCoroutine(WaitAttack());
    }

    private IEnumerator WaitAttack() {
        yield return null;
        animator.SetBool("isAttacking", false);
        yield return new WaitForSeconds(weapon.AttackDuration);
        state = State.CHASE;
    }
}