import urllib.request
import json
import time
import re
import random
from pathlib import Path
import requests

# 전역 변수로 세션 관리
session = requests.Session()
MODEL_NAME = "gemma3"
API_URL = 'http://localhost:11434/api/generate'

# ==============================
#  서버 호출 함수
# ==============================
def get_response(prompt: str, api_url: str = API_URL) -> str:
    """
    prompt 문자열을 LLM 서버에 보내고,
    JSON 응답에서 'response' 필드를 꺼내 출력 후 반환합니다.
    세션을 재사용하여 모델 로딩 오버헤드를 방지합니다.
    """
    payload = {
        "model": MODEL_NAME,
        "prompt": prompt,
        "stream": False  # 스트리밍 모드를 꺼서 단일 JSON 응답을 받습니다
    }

    # 요청-응답 시간 측정 시작
    start_time = time.time()
    
    # 요청 전송 시간 측정
    request_start = time.time()
    response = session.post(api_url, json=payload)
    request_time = time.time() - request_start
    
    # 응답 처리 시간 측정
    process_start = time.time()
    result = response.json()
    answer = result.get("response", "")
    process_time = time.time() - process_start
    
    # 전체 시간
    total_time = time.time() - start_time

    # 개행 문자 및 백슬래시 제거: 실제 newline, JSON 이스케이프된 "\n" 모두 처리
    answer = answer.replace("\\n", " ")
    answer = answer.replace("\n", " ")
    # 모든 백슬래시 제거
    answer = answer.replace("\\", "")
    # 코드 펜스(markdown) 제거
    answer = answer.replace("```json", "")
    answer = answer.replace("```", "")
    # 중복 공백 정리
    answer = re.sub(r'\s+', ' ', answer).strip()

    # 결과 출력
    print("🧠 응답:", answer)
    print(f"⏱ 시간 측정:")
    print(f"  - 요청 전송: {request_time:.3f}초")
    print(f"  - 응답 처리: {process_time:.3f}초")
    print(f"  - 전체 시간: {total_time:.3f}초")
    print(f"  - 응답 헤더: {dict(response.headers)}")

    return answer

# ==============================
#  프롬프트 조립 함수
# ==============================
def preprocess_agent_data(state_obj: dict) -> dict:
    """
    state_obj 안의 agent 데이터를 전처리합니다:
      1) 숫자 state → "very/not X" 문자열로 변환
      2) visible_objects, interactable_items → "[obj] located in [location]" 포맷으로 변환
      3) memories DB에서 읽어서 정렬 후 리스트로 추가
      4) timestamp, random_seed 추가
    반환값: 변환된 필드를 포함해 확장된 state_obj
    """
    # 1) timestamp, seed
    state_obj["_current_timestamp"] = time.strftime("%H:%M:%S")
    state_obj["_random_seed"] = random.randint(1, 10_000)

    # 2) load memory DB
    mem_path = Path("./memories/agents_memories.json")
    if mem_path.exists():
        memory_db = json.loads(mem_path.read_text(encoding="utf-8"))
    else:
        memory_db = {}

    # 3) agent 데이터 변환
    agent = state_obj.get("agent", {})
    
    # 3-1) state → 문자열
    state_map = {
        'hunger': 'hungry',
        'sleepiness': 'sleepy',
        'loneliness': 'lonely',
        'stress': 'stressed',
        'happiness': 'happy'
    }
    phrases = []
    for k, v in agent.get("state", {}).items():
        base = state_map.get(k, k)
        prefix = "not " if v <= 3 else "very " if v > 6 else ""
        phrases.append(f"{prefix}{base}")
    state_obj["state_desc"] = ", ".join(phrases)

    # 3-2) visible_objects
    vis_list = []
    for grp in agent.get("visible_objects", []):
        loc = grp.get("location", agent.get("location", "unknown"))
        for obj in grp.get("objects", []):
            vis_list.append(f"{obj} located in {loc}")
    state_obj["visible_desc"] = ", ".join(vis_list)

    # 3-3) interactable_items
    int_list = []
    for grp in agent.get("interactable_items", []):
        loc = grp.get("location", agent.get("location", "unknown"))
        for obj in grp.get("objects", []):
            int_list.append(f"{obj} located in {loc}")
    state_obj["interact_desc"] = ", ".join(int_list)

    # 4) 기본 필드 추가
    state_obj["name"] = agent.get("name", "unknown")
    state_obj["location"] = agent.get("location", "unknown")

    return state_obj

def load_prompt_template() -> str:
    """
    prompt.txt 파일에서 프롬프트 템플릿을 읽어옵니다.
    """
    try:
        with open('./prompts/prompt.txt', 'r', encoding='utf-8') as f:
            return f.read().strip()
    except FileNotFoundError:
        raise FileNotFoundError("prompt.txt 파일을 찾을 수 없습니다.")

def format_prompt(state_obj: dict) -> str:
    """
    state_obj: 에이전트 상태가 담긴 dict

    프롬프트 템플릿에 데이터를 주입합니다.
    """
    # 1) state_obj 전처리
    enriched = preprocess_agent_data(state_obj)
    
    # 2) 프롬프트 템플릿 로드
    prompt_template = load_prompt_template()
    
    # 3) 기본 형식 적용
    try:
        formatted_prompt = prompt_template.format(**enriched)
    except KeyError as e:
        print(f"Warning: Missing key in state object: {e}")
        missing_key = str(e.args[0])
        formatted_prompt = prompt_template.replace(f"{{{missing_key}}}", f"unknown_{missing_key}")

    print("🧠 프롬프트:", formatted_prompt)
    return formatted_prompt

# 이 모듈을 import 해서 사용하세요:
#
# from api_client import get_response, format_prompt
#
# 예시:
# instruction = open('prompt.txt', encoding='utf-8').read()
# state_data = json.loads(external_json_string)
# prompt = format_prompt(state_data)
# answer = get_response(prompt, 'http://localhost:11434/api/generate')
