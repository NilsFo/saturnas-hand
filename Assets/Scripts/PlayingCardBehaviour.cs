using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class PlayingCardBehaviour : MonoBehaviour
{
    public enum PlayingCardState
    {
        Unknown,
        DrawAnimation,
        Drawing,
        InHand,
        Selected,
        Played,
        Destroyed
    }

    public enum PlayingCardEffect
    {
        None,
        GivesAdjacentSigilDoublePoints,
        ReturnToHand,
        GivesAdjacentDemonDoublePoints,
        GivesDoubleDemon
    }

    [Header("World Hookup")] public PlayingCardData playingCardData;
    public Vector3 handWorldPos;
    private GameState _gameState;
    public Vector3 playedWorldPos;

    [Header("Card visual design")] public TMP_Text nameTF;
    public TMP_Text powerTF;
    public TMP_Text descriptionTF;
    public UnityEngine.UI.Image artworkImg;
    public UnityEngine.UI.Image backgroundImg;
    public Sigil sigil;
    public float drawAnimationDuration = .5f;
    public UnityEngine.UI.Image burnMask;
    public ParticleSystem smokeParticlePrefab;
    public ParticleSystem fireParticlePrefab;

    [Header("Card visual values")] public string displayName;
    public int basePower;
    public Vector2 sigilDirection;
    public Sprite sprite;

    [Header("Card visual defaults")] public Color titleTextColorDefault = new Color(60 / 255f, 51 / 255f, 76 / 255f);
    public Color titleTextColorFoil = new Color(42 / 255f, 73 / 255f, 41 / 255f);
    public Color titleTextColorDemonic = new Color(114 / 255f, 54 / 255f, 39 / 255f);
    public Sprite cardBackgroundDefault;
    public Sprite cardBackgroundSinged;

    [Header("Gameplay Modifiers")] public PlayingCardState playingCardState;
    private PlayingCardState _playingCardState;
    public bool isBurned = false;
    public bool isBloodSoaked = false;
    public bool isFoil = false;
    public bool isDaemon = false;

    [Header("Card Effects")] public PlayingCardEffect cardEffect;
    public bool IsEffectCard => cardEffect != PlayingCardEffect.None;
    public float powerMod = 1f;
    public bool returnToHandAtEndOfRound = false;

    [Header("Parameters")] public float movementSpeed = 7;
    public float rotationSpeed = 10;
    public float selectionHoverDistance = 0.6f;

    [Header("Readonly")] public bool inTransition;

    [Header("Hooks")] public UnityEvent<PlayingCardBehaviour> onDestroy;

    [Header("SFX")] public AudioClip cardReturnToHandClip;
    public AudioClip cardDragClip;
    public AudioClip placeOnTableSound;
    public AudioClip demonDestroyed;
    public AudioClip normalCardDestroyed;
    
    private void Awake()
    {
        _gameState = FindObjectOfType<GameState>();
        cardEffect = PlayingCardEffect.None;

        if (onDestroy == null)
            onDestroy = new UnityEvent<PlayingCardBehaviour>();
    }

    // Start is called before the first frame update
    void Start()
    {
        _playingCardState = playingCardState;
        _gameState = FindObjectOfType<GameState>();

        if (playingCardData != null)
        {
            displayName = playingCardData.cardName;
            basePower = playingCardData.PowerScala();
            sprite = playingCardData.sprite;
            sigilDirection = playingCardData.sigilDirection;
            cardEffect = playingCardData.cardEffect;
        }

        if (Random.Range(0f, 1f) <= _gameState.cardFoilChance)
        {
            isFoil = true;
        }

        if (IsEffectCard || isDaemon)
        {
            isFoil = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playingCardState == PlayingCardState.Unknown)
        {
            Debug.LogError("Unknown card state");
            return;
        }

        if (_playingCardState != playingCardState)
        {
            _playingCardState = playingCardState;
            //Debug.Log("New card state: " + playingCardState);
            if (_playingCardState == PlayingCardState.Drawing)
            {
                var tween = transform.DOMove(handWorldPos, drawAnimationDuration).SetEase(Ease.OutCubic);
                tween.OnUpdate(() => { tween.SetTarget(handWorldPos); });
                tween.OnComplete(() =>
                {
                    //Debug.Log("Tween complete");
                    playingCardState = PlayingCardState.InHand;
                });
                tween.Play();

                transform.DORotateQuaternion(
                    Quaternion.LookRotation(
                        _gameState.camera.transform.forward,
                        Vector3.right),
                    drawAnimationDuration).Play();
            }
            else if (_playingCardState == PlayingCardState.Selected)
            {
                transform.DORotateQuaternion(
                    Quaternion.LookRotation(Vector3.down, Vector3.right),
                    drawAnimationDuration).Play();
            }
        }

        transform.DOPlay();
        var rot = transform.rotation.eulerAngles;
        switch (playingCardState)
        {
            case PlayingCardState.Drawing:
                /*transform.position =
                    Vector3.MoveTowards(transform.position, handWorldPos, Time.deltaTime * movementSpeed);*/
                inTransition = !(Vector3.Distance(transform.position, handWorldPos) <= 0.01f);
                break;
            case PlayingCardState.DrawAnimation:
                //playingCardState = PlayingCardState.Drawing;
                break;
            case PlayingCardState.Played:
                transform.position =
                    Vector3.MoveTowards(transform.position, playedWorldPos, Time.deltaTime * rotationSpeed);
                inTransition = !(Vector3.Distance(transform.position, playedWorldPos) <= 0.01f);

                // rotation
                var f = transform.forward;
                var r = -Vector3.up;
                var l = Vector3.RotateTowards(f, r, Time.deltaTime * movementSpeed, 0.0f);
                transform.rotation = Quaternion.LookRotation(l, Vector3.right);

                break;
            case PlayingCardState.Selected:
                // Updating mouse pos if selected

                Vector3 mousePos = _gameState.mouseCardPlaneTargetPos;
                if (_gameState.mouseSelectHasTarget)
                {
                    mousePos += _gameState.camera.transform.forward * selectionHoverDistance;
                }

                transform.position += (mousePos - transform.position) * (10f * Time.deltaTime);
                break;
            case PlayingCardState.InHand:
                transform.position =
                    Vector3.MoveTowards(transform.position, handWorldPos, Time.deltaTime * movementSpeed);
                inTransition = !(Vector3.Distance(transform.position, handWorldPos) <= 0.01f);

                // rotation
                transform.rotation = Quaternion.Euler(Vector3.MoveTowards(rot,
                    _gameState.camera.transform.rotation.eulerAngles, Time.deltaTime * rotationSpeed));

                break;
            case PlayingCardState.Destroyed:
                break;
            default:
                playingCardState = PlayingCardState.Unknown;
                break;
        }

        // Draw placement arrow
        if (_gameState.playingState == GameState.PlayingState.CardDrag && playingCardState == PlayingCardState.Selected)
        {
            Debug.DrawLine(transform.position, _gameState.mouseSelectTargetPos, Color.red);

            // Placing the card
            if (Input.GetMouseButtonDown(0) && _gameState.AllowDropping)
            {
                PlaceOnTable();
            }
        }

        // Updating card art
        UpdateVisuals();

        gameObject.name = GetName() + ": " + GetPower() + " -> " + GetSigilDirection();
    }

    private void UpdateVisuals()
    {
        nameTF.text = GetName();
        powerTF.text = GetPower().ToString();
        descriptionTF.text = GetEffectDescription();

        if (sprite != null)
        {
            artworkImg.sprite = sprite;
            artworkImg.enabled = true;
        }
        else
            artworkImg.enabled = false;

        sigil.dir = GetSigilDirection();
        sigil.UpdateSigilSprite();
        if (isBurned)
            backgroundImg.sprite = cardBackgroundSinged;
        else
            backgroundImg.sprite = cardBackgroundDefault;

        if (isFoil)
            nameTF.color = titleTextColorFoil;
        else if (isDaemon)
            nameTF.color = titleTextColorDemonic;
        else
            nameTF.color = titleTextColorDefault;
    }

    public void OnClick()
    {
        if (_gameState.levelState != GameState.LevelState.Playing &&
            !(_gameState.levelState == GameState.LevelState.Summoning && isDaemon))
        {
            Debug.LogWarning("Cannot select this card. Wrong game state.");
            return;
        }

        Debug.Log("Clicked on: " + name);
        if (_gameState.playingState == GameState.PlayingState.Default)
        {
            // Placing card on board
            if (playingCardState == PlayingCardState.InHand)
            {
                DragCard();
            }

            // Returning card to hand
            if (playingCardState == PlayingCardState.Played && _gameState.allowCardPickUp)
            {
                DragCard();
            }
        }

        if (playingCardState == PlayingCardState.DrawAnimation && isDaemon)
        {
            if (_gameState.demonCaptureCorrect)
            {
                playingCardState = PlayingCardState.Drawing;
                _gameState.demonCaptureCount += 1;
            }
            else
            {
                _gameState.musicManager.CreateAudioClip(demonDestroyed,_gameState.transform.position,respectBinning:false);
                DestroyCard();
            }

            var otherDaemon = _gameState.handGameObject.cardsInHand.Find((card) =>
                card.isDaemon && card.playingCardState == PlayingCardState.DrawAnimation);
            if (otherDaemon == null)
            {
                // No other daemons, start next round
                Invoke("EndTheRound", 1f);
            }
        }
    }

    private void EndTheRound()
    {
        _gameState.levelState = GameState.LevelState.EndOfRound;
    }

    public void ReturnToHand()
    {
        Debug.Log("Returning to hand: " + name);
        playingCardState = PlayingCardState.Drawing;

        _gameState.playingState = GameState.PlayingState.Default;
        if (!_gameState.handGameObject.cardsInHand.Contains(this))
        {
            _gameState.handGameObject.cardsInHand.Add(this);
            handWorldPos =
                _gameState.handGameObject.GetDesiredCardPosition(_gameState.handGameObject.cardsInHand.Count - 1);
        }
    }

    public void DragCard()
    {
        Debug.Log("card selected: " + name);
        _gameState.musicManager.CreateAudioClip(cardDragClip, _gameState.transform.position, respectBinning: false);
        _gameState.DragCard(this);
        playingCardState = PlayingCardState.Selected;
    }

    public void PlaceOnTable()
    {
        if (_gameState.mouseSelectTargetObj == null)
        {
            Debug.LogWarning("Cannot place. Nothing selected.");
            return;
        }

        // Notifying listeners
        _gameState.mouseSelectTargetObj.Select(this);

        // Updating stuff
        playedWorldPos = _gameState.mouseSelectTargetObj.transform.position;
        SelectorTarget targetObj = _gameState.mouseSelectTargetObj;

        if (targetObj.placeable)
        {
            _gameState.musicManager.CreateAudioClip(placeOnTableSound,_gameState.transform.position,respectBinning:false);
            _gameState.playingState = GameState.PlayingState.Default;
            playingCardState = PlayingCardState.Played;
            _gameState.handGameObject.cardsInHand.Remove(this);
        }
        else
        {
            ReturnToHand();
        }
    }

    public void OnRoundEnd()
    {
        if (playingCardState == PlayingCardState.Played)
        {
            Debug.Log("Removing played card " + name);
            if (returnToHandAtEndOfRound)
            {
                ReturnToHand();
            }
            else
            {
                DestroyCard();
            }
        }
    }

    public void DestroyCard()
    {
        if (playingCardState == PlayingCardState.Selected)
        {
            _gameState.playingState = GameState.PlayingState.Default;
        }

        playingCardState = PlayingCardState.Destroyed;

        if (!isDaemon)
        {
            _gameState.musicManager.CreateAudioClip(normalCardDestroyed,_gameState.transform.position,respectBinning:true);
        }

        _gameState.handGameObject.cardsInHand.Remove(this);

        burnMask.enabled = true;
        burnMask.rectTransform.DOSizeDelta(Vector2.zero, 1.2f)
            .OnComplete(() =>
            {
                onDestroy?.Invoke(this);
                Destroy(gameObject);
            })
            .Play();
        Instantiate(smokeParticlePrefab, transform);
        Instantiate(fireParticlePrefab, transform);
    }

    private void OnDestroy()
    {
        _gameState.handGameObject.cardsInHand.Remove(this);
    }

    public float GetPower()
    {
        float power = basePower;

        if (isFoil)
        {
            power = Mathf.CeilToInt(((float)power * _gameState.cardFoilMult));
        }

        if (isBurned)
        {
            power = Mathf.CeilToInt(((float)power * _gameState.cardBurningMult));
        }

        return (int)Mathf.CeilToInt(MathF.Max(power * powerMod, 0));
    }

    public Vector2 GetSigilDirection()
    {
        Vector2 v = sigilDirection;

        if (isBurned)
        {
            v.x = v.x * -1;
            v.y = v.y * -1;
        }

        return v.normalized;
    }

    public string GetName()
    {
        string name = displayName;
        if (isBurned)
        {
            name = "Singed " + name;
        }

        if (isFoil)
        {
            name = name + "";
        }

        return name;
    }

    public string GetEffectDescription()
    {
        switch (cardEffect)
        {
            case PlayingCardEffect.None:
                return "";
            case PlayingCardEffect.GivesDoubleDemon:
                return "Summon +1 demon";
            case PlayingCardEffect.ReturnToHand:
                return "Return cards to hand (except this)";
            case PlayingCardEffect.GivesAdjacentDemonDoublePoints:
                return "Boosts adjacent demon cards.";
            case PlayingCardEffect.GivesAdjacentSigilDoublePoints:
                return "Boosts adjacent cards with aligned sigils.";
            default:
                Debug.LogWarning("Error! Unknown card state!");
                return "<unknown>";
        }
    }
}