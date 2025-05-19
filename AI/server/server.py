# server.py
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
import json
import re
import asyncio
import sys
from pathlib import Path
import os
import shutil
from datetime import datetime, timedelta
import time
import gensim.downloader as api
from typing import Dict, Any
import numpy as np

print("\n=== 서버 초기화 시작 ===")
start_time = time.time()

# 현재 파일의 절대 경로를 기준으로 상위 디렉토리 찾기
CURRENT_DIR = Path(__file__).parent
ROOT_DIR = CURRENT_DIR.parent  # AI 디렉토리
print(f"📁 작업 디렉토리: {ROOT_DIR}")

# AI 디렉토리를 Python 경로에 추가
if str(ROOT_DIR) not in sys.path:
    sys.path.append(str(ROOT_DIR))
    print(f"📌 Python 경로에 추가됨: {ROOT_DIR}")

print("\n=== 모듈 임포트 시작 ===")
import_start = time.time()

try:
    from agent.modules.ollama_client import OllamaClient
    print("✅ OllamaClient 임포트 완료")
except Exception as e:
    print(f"❌ OllamaClient 임포트 실패: {e}")

try:
    from agent.modules.memory_utils import MemoryUtils
    print("✅ MemoryUtils 임포트 완료")
except Exception as e:
    print(f"❌ MemoryUtils 임포트 실패: {e}")

try:
    from agent.modules.retrieve import MemoryRetriever
    print("✅ MemoryRetriever 임포트 완료")
except Exception as e:
    print(f"❌ MemoryRetriever 임포트 실패: {e}")

try:
    from agent.modules.embedding_updater import EmbeddingUpdater
    print("✅ EmbeddingUpdater 임포트 완료")
except Exception as e:
    print(f"❌ EmbeddingUpdater 임포트 실패: {e}")

from agent.modules.reaction_decider import ReactionDecider
from agent.modules.npc_conversation import NPCConversationManager

# feedback_processor 모듈 임포트
try:
    from agent.modules.feedback_processor import FeedbackProcessor
    print("✅ FeedbackProcessor 임포트 완료")
except Exception as e:
    print(f"❌ FeedbackProcessor 임포트 실패: {e}")

# simple_feedback_processor 모듈 임포트
try:
    from agent.modules.simple_feedback_processor import SimpleFeedbackProcessor
    print("✅ SimpleFeedbackProcessor 임포트 완료")
except Exception as e:
    print(f"❌ SimpleFeedbackProcessor 임포트 실패: {e}")

try:
    from agent.modules.reflection.importance_rater import ImportanceRater
    from agent.modules.reflection.reflection_pipeline import process_reflection_request
    from agent.modules.plan.plan_pipeline import process_plan_request
    print("✅ reflection 및 plan 모듈 임포트 완료")
except Exception as e:
    print(f"❌ reflection 및 plan 모듈 임포트 실패: {e}")

print(f"⏱ 모듈 임포트 시간: {time.time() - import_start:.2f}초")

app = FastAPI()

# CORS 설정
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

print("\n=== 모듈 인스턴스 생성 시작 ===")
instance_start = time.time()

# Word2Vec 모델 로드
print("🤖 Word2Vec 모델 로딩 중...")
word2vec_model = api.load('word2vec-google-news-300')
print("✅ Word2Vec 모델 로딩 완료")

# object_embeddings.json 파일 로드
print("📚 object_embeddings.json 파일 로딩 중...")
object_embeddings_path = ROOT_DIR / "agent" / "data" / "object_dict" / "object_embeddings.json"
try:
    with open(object_embeddings_path, 'r', encoding='utf-8') as f:
        object_embeddings = json.load(f)
    print("✅ object_embeddings.json 파일 로딩 완료")
except Exception as e:
    print(f"❌ object_embeddings.json 파일 로딩 실패: {e}")
    object_embeddings = {}

try:
    client = OllamaClient()
    print("✅ OllamaClient 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ OllamaClient 인스턴스 생성 실패: {e}")

try:
    memory_utils = MemoryUtils(word2vec_model)
    print("✅ MemoryUtils 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ MemoryUtils 인스턴스 생성 실패: {e}")

try:
    retrieve = MemoryRetriever(memory_file_path="agent/data/memories.json", word2vec_model=word2vec_model)
    print("✅ MemoryRetriever 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ MemoryRetriever 인스턴스 생성 실패: {e}")

try:
    embedding_updater = EmbeddingUpdater(word2vec_model)
    print("✅ EmbeddingUpdater 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ EmbeddingUpdater 인스턴스 생성 실패: {e}")

try:
    reaction_decider = ReactionDecider(
        memory_utils=memory_utils,
        ollama_client=client,
        word2vec_model=word2vec_model
    )
    print("✅ ReactionDecider 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ ReactionDecider 인스턴스 생성 실패: {e}")

try:
    conversation_manager = NPCConversationManager(
        ollama_client=client,
        memory_utils=memory_utils,
        word2vec_model=word2vec_model,
        max_turns=4  # 모듈 내부에서 최대 턴 수 설정 (필요에 따라 변경 가능)
    )
    print("✅ NPCConversationManager 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ NPCConversationManager 인스턴스 생성 실패: {e}")

# feedback_processor 인스턴스 생성
try:
    feedback_processor = FeedbackProcessor(
        memory_utils=memory_utils,
        ollama_client=client
    )
    print("✅ FeedbackProcessor 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ FeedbackProcessor 인스턴스 생성 실패: {e}")

try:
    simple_feedback_processor = SimpleFeedbackProcessor(
        memory_utils=memory_utils
    )
    print("✅ SimpleFeedbackProcessor 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ SimpleFeedbackProcessor 인스턴스 생성 실패: {e}")

print(f"⏱ 인스턴스 생성 시간: {time.time() - instance_start:.2f}초")

# 프롬프트 템플릿
RETRIEVE_PROMPT_TEMPLATE = """
당신은 {AGENT_NAME}입니다. 현재 상황에 대해 반응해야 합니다.

현재 이벤트:
{EVENT_CONTENT}

유사한 과거 이벤트:
{SIMILAR_EVENT}

다음과 같은 형식으로 JSON 응답을 해주세요:
{{
    "action": "이벤트에 대한 반응",
    "emotion": "감정 상태",
    "reason": "반응 이유"
}}
"""

RETRIEVE_SYSTEM_TEMPLATE = """
You are a helpful AI assistant that responds in JSON format.
Your responses should be natural and contextual.
"""

# 프롬프트 파일 경로
PROMPT_DIR = ROOT_DIR / "agent" / "prompts" / "retrieve"
RETRIEVE_PROMPT_PATH = PROMPT_DIR / "retrieve_prompt.txt"
RETRIEVE_SYSTEM_PATH = PROMPT_DIR / "retrieve_system.txt"

print("\n=== 프롬프트 파일 확인 ===")
print(f"📂 프롬프트 디렉토리: {PROMPT_DIR}")
print(f"📄 프롬프트 파일: {RETRIEVE_PROMPT_PATH}")
print(f"📄 시스템 파일: {RETRIEVE_SYSTEM_PATH}")

def load_prompt_file(file_path: Path) -> str:
    """
    프롬프트 파일을 로드하거나 기본 템플릿을 반환
    
    Args:
        file_path: 프롬프트 파일 경로
    
    Returns:
        str: 프롬프트 내용
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return f.read()
    except Exception as e:
        print(f"❌ 프롬프트 파일 로드 중 오류 발생: {e}")
        # 파일이 없는 경우 기본 템플릿 반환
        if file_path == RETRIEVE_PROMPT_PATH:
            return RETRIEVE_PROMPT_TEMPLATE
        elif file_path == RETRIEVE_SYSTEM_PATH:
            return RETRIEVE_SYSTEM_TEMPLATE
        return ""

@app.get("/hello")
async def hello():
    return "Hello from Python!"

@app.post("/perceive")
async def perceive_event(payload: dict):
    """관찰 정보를 저장하는 엔드포인트"""
    try:
        if not payload or 'agent' not in payload:
            return {"success": False, "error": "agent field is required"}
            
        agent_data = payload.get('agent', {})
        agent_name = agent_data.get('name', 'John')
        event_data = agent_data.get('perceive_event', {})
        
        # 게임 시간 가져오기
        game_time = agent_data.get('time', None)
        
        # 시간 정보가 없으면 추가
        if game_time and "time" not in event_data:
            event_data["time"] = game_time
        
        # 메모리 저장
        success = False
        if event_data.get("event_is_save", True):
            success = memory_utils.save_perception(event_data, agent_name)
        else:
            print("💾 event_is_save 값이 False이므로 메모리 저장 건너뜀")
        return {
            "success": success
        }
        
    except Exception as e:
        print(f"❌ 관찰 정보 저장 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}

@app.post("/location_data")
async def location_data(payload: dict):
    """관찰 정보를 저장하는 엔드포인트"""
    try:
        if not payload or 'agent' not in payload:
            return {"success": False, "error": "agent field is required"}
            
        agent_data = payload.get('agent', {})
        agent_name = agent_data.get('name', 'John')
        event_data = agent_data.get('perceive_event', {})
        
        # 게임 시간 가져오기
        game_time = agent_data.get('time', None)
        
        # 시간 정보가 없으면 추가
        if game_time and "time" not in event_data:
            event_data["time"] = game_time
        
        # 메모리 저장
        success = False
        if event_data.get("event_is_save", True):
            success = memory_utils.save_location_data(event_data, agent_name)
        else:
            print("💾 event_is_save 값이 False이므로 메모리 저장 건너뜀")
        return {
            "success": success
        }
        
    except Exception as e:
        print(f"❌ 관찰 정보 저장 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}


@app.post("/react")
async def should_react(payload: dict):
    """관찰된 이벤트에 반응할지 여부를 결정하는 엔드포인트"""
    try:
        # 전체 처리 시작 시간 기록
        react_start_time = time.time()
        print("\n=== /react 엔드포인트 호출 ===")
        print("📥 요청 데이터:", json.dumps(payload, indent=2, ensure_ascii=False))
        
        if not payload or 'agent' not in payload:
            return {"success": False, "error": "agent field is required"}
            
        agent_data = payload.get('agent', {})
        agent_name = agent_data.get('name', 'John')
        event_data = agent_data.get('perceive_event', {})
        
        # 게임 시간 가져오기
        game_time = agent_data.get('time', None)
        
        # 이벤트 데이터에 time 추가
        if game_time and "time" not in event_data:
            event_data["time"] = game_time
        
        # 반응 여부 판단
        print("🤔 반응 여부 판단 중...")
        decision_start = time.time()
        reaction_decision = await reaction_decider.should_react_to_event(event_data, agent_data)
        decision_time = time.time() - decision_start
        print(f"⏱ 반응 판단 시간: {decision_time:.2f}초")
        
        # 결과 추출 - 단순 불리언 값과 이유
        should_react = reaction_decision.get("should_react", True)
        reason = reaction_decision.get("reason", "")

        # 메모리 저장 (실패했을 경우만 저장)
        ## 실패시에만 저장하는 이유는 성공했을 때 make_reaction에서 저장하기 때문
        ### event_is_save 파라미터를 통해 저장 여부를 결정하는 것도 추가
        event_is_save = event_data.get("event_is_save", True)
        if should_react == False and event_is_save == True:
            print("💾 메모리 저장 중...")
            memory_start = time.time()
            success = memory_utils.save_perception(event_data, agent_name)
            memory_time = time.time() - memory_start
            print(f"⏱ 메모리 저장 시간: {memory_time:.2f}초")
        
        # 전체 처리 시간 계산
        total_time = time.time() - react_start_time
        print(f"⏱ 전체 처리 시간: {total_time:.2f}초")
        
        # 응답 - 단순 형식으로 반환
        return {
            "success": True,
            "should_react": should_react  # True 또는 False
        }
        
    except Exception as e:
        print(f"❌ 반응 결정 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}


@app.post("/make_reaction")
async def react_to_event(payload: dict):
    """이벤트에 대한 반응을 생성하는 엔드포인트"""
    try:
        # 전체 처리 시작 시간 기록
        total_start_time = time.time()
        
        # 요청 데이터 로깅
        print("\n=== /make_reaction 엔드포인트 호출 ===")
        print("📥 요청 데이터:", json.dumps(payload, indent=2, ensure_ascii=False))
        
        # 필수 필드 확인
        if not payload or 'agent' not in payload:
            print("❌ 필수 필드 누락")
            return {"error": "agent field is required"}, 400
            
        # 에이전트 데이터 추출
        agent_data = payload.get('agent', {})
        agent_name = agent_data.get('name', 'John')
        
        # 이벤트 데이터 추출
        event_data = agent_data.get('perceive_event', {})
        event_type = event_data.get('event_type', '')
        event_location = event_data.get('event_location', '')
        event_description = event_data.get('event_description', '')
        event_role = event_data.get('event_role', '')
        event_is_save = event_data.get("event_is_save", True)
        event_importance = event_data.get("importance", 0)
        
        # 에이전트의 현재 시간 추출
        agent_time = agent_data.get('time', '')
        if not agent_time:
            agent_time = datetime.now().strftime("%Y.%m.%d.%H:%M")
        
        # 시간 정보가 없으면 추가
        if "time" not in event_data:
            event_data["time"] = agent_time
        
        print(f"👤 에이전트 이름: {agent_name}")
        print(f"🔍 이벤트 타입: {event_type}")
        print(f"📍 이벤트 위치: {event_location}")
        print(f"⏰ 에이전트 시간: {agent_time}")
        print(f"🧩 성격: {agent_data.get('personality', 'None')}")
        print(f"📍 현재 위치: {agent_data.get('current_location', 'None')}")
        print(f"🔍 이벤트 저장 여부: {event_is_save}")
        print(f"🔍 이벤트 주체: {event_role}")
        
        visible_interactables = agent_data.get('visible_interactables', [])
        if visible_interactables:
            print("👁️ 상호작용 가능한 객체:")
            for loc_data in visible_interactables:
                loc = loc_data.get('location', '')
                objects = loc_data.get('interactables', [])
                print(f"  - {loc}: {', '.join(objects)}")
        
        # 이벤트 객체 생성
        event = {
            "event_type": event_type,
            "event_location": event_location,
            "time": agent_time,  # 시간 정보 추가
            "event_description": event_description,
            "event_role": event_role
        }
        
        # 이벤트를 문장으로 변환
        event_sentence = memory_utils.event_to_sentence(event)
        print(f"📝 이벤트 문장: {event_sentence}")
        
        # 임베딩 생성
        embedding = memory_utils.get_embedding(event_sentence)
        print(f"🔢 임베딩 생성 완료 (차원: {len(embedding)})")

        # 상태 임베딩 생성
        state_str = retrieve._format_state(agent_data.get("state", {})) if agent_data and "state" in agent_data else ""
        state_embedding = memory_utils.get_embedding(state_str) if state_str else embedding
        print(f"🔢 상태 임베딩 생성 완료 (차원: {len(state_embedding)})")

        # 프롬프트 생성
        prompt = retrieve.create_reaction_prompt(
            event_sentence=event_sentence,
            event_role=event_role,
            event_embedding=embedding,
            state_embedding=state_embedding,
            agent_name=agent_name,
            prompt_template=load_prompt_file(RETRIEVE_PROMPT_PATH),
            agent_data=agent_data,
            similar_data_cnt=5,  # 유사한 이벤트 5개 포함
            similarity_threshold=0.1,  # 유사도 0.5 이상인 이벤트만 포함
            object_embeddings=object_embeddings
        )
        print(f"📋 생성된 프롬프트:\n{prompt}")
        
        # Ollama API 호출
        print("🤖 Ollama API 호출 중...")
        
        # Ollama 호출 시작 시간 기록
        ollama_start_time = time.time()
        
        try:
            # Ollama API 호출
            response = await client.process_prompt(
                prompt=prompt,
                system_prompt=load_prompt_file(RETRIEVE_SYSTEM_PATH),
                model_name="gemma3",
                options={
                    "temperature": 0.7,
                    "top_p": 0.9,
                    "frequency_penalty": 0.1,
                    "presence_penalty": 0.1
                }
            )
            
            # Ollama 응답 시간 계산
            ollama_response_time = time.time() - ollama_start_time
            
            if response.get("status") != "success":
                raise HTTPException(status_code=500, detail=f"Ollama API 호출 실패: {response.get('status')}")
            
            answer = response.get("response", "")
            print(f"📥 Ollama 응답: {answer}")
            
            # 1) 펜스 제거
            cleaned = answer.replace("```json", "").replace("```", "").strip()
            print(f"🧹 정제된 응답: {cleaned}")
            
            # 2) JSON 텍스트 추출 (더 유연한 패턴)
            match = re.search(r'\{[\s\S]*\}', cleaned)
            if not match:
                print("❌ JSON 형식이 아닙니다.")
                raise HTTPException(status_code=500, detail="응답에서 JSON을 찾을 수 없습니다.")
            
            json_text = match.group(0)
            print(f"📄 추출된 JSON: {json_text}")

            # 3) 파싱
            reaction_obj = json.loads(json_text)
            print(f"✅ JSON 파싱 성공: {reaction_obj}")
            
            # # 필수 필드 확인
            # if "action" not in reaction_obj or "details" not in reaction_obj:
            #     print("⚠️ 응답에 필수 필드가 없습니다. 기본값으로 대체합니다.")
            #     if "action" not in reaction_obj:
            #         reaction_obj["action"] = "use"
            #     if "details" not in reaction_obj:
            #         reaction_obj["details"] = {
            #             "location": " ",
            #             "target": " ",
            #             "duration": " ",
            #             "reason": " "
            #         }
                
            if event_is_save == False:
                event_sentence = ""
                event_importance = 0
                embedding = memory_utils.get_embedding("")

            memory_id = memory_utils.save_memory(
                event_sentence=event_sentence,
                embedding=embedding,
                event_time=agent_time,  # 에이전트의 시간 사용
                agent_name=agent_name,
                event_role=event_role,
                importance=event_importance
            )
            print(f"💾 메모리 저장 완료 (시간: {agent_time}, 메모리 ID: {memory_id})")

            # 전체 처리 시간 계산
            total_response_time = time.time() - total_start_time
            
            # 시간 측정 결과 출력
            print(f"\n⏱ 시간 측정 결과:")
            print(f"  - Ollama 응답 시간: {ollama_response_time:.2f}초")
            print(f"  - 전체 처리 시간: {total_response_time:.2f}초")
            
            # 메모리 ID를 응답에 포함
            reaction_obj["memory_id"] = memory_id
            
            return {
                "success": True,
                "data": reaction_obj
            }
            
        except json.JSONDecodeError as e:
            print(f"❌ JSON 파싱 실패: {e}")
            raise HTTPException(status_code=500, detail=f"JSON 파싱 실패: {e}")
        except Exception as e:
            print(f"❌ 응답 처리 중 오류: {e}")
            raise HTTPException(status_code=500, detail=str(e))
        
    except Exception as e:
        print(f"❌ 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}, 500


@app.post("/simple_action_feedback")
async def save_simple_action_feedback(payload: dict):
    """LLM을 사용하지 않고 행동에 대한 피드백을 저장하는 엔드포인트"""
    try:
        # 전체 처리 시작 시간 기록
        start_time = time.time()
        print("\n=== /simple_action_feedback 엔드포인트 호출 ===")
        print("📥 요청 데이터:", json.dumps(payload, indent=2, ensure_ascii=False))
        
        if not payload or 'agent' not in payload:
            return {"success": False, "error": "agent field is required"}
            
        # 피드백 처리
        result = simple_feedback_processor.process_simple_feedback(payload)
        
        if not result:
            return {"success": False, "error": "Failed to process feedback"}
        
        # 처리 시간 계산
        total_time = time.time() - start_time
        print(f"⏱ 피드백 처리 시간: {total_time:.2f}초")
        
        return result
        
    except Exception as e:
        print(f"❌ 간단한 피드백 저장 중 오류 발생: {str(e)}")
        import traceback
        traceback.print_exc()
        return {"success": False, "error": str(e)}


@app.post("/reflect-and-plan")
async def reflection_and_plan(payload: Dict[str, Any]):
    """반성 및 계획 생성 엔드포인트"""
    try:
        # 전체 처리 시작 시간 기록
        total_start_time = time.time()
        print(f"\n=== /reflect-and-plan 엔드포인트 호출 ===")
        print(f"📥 요청 데이터: {payload}")
        
        # 필수 필드 확인
        if "agent" not in payload or "name" not in payload["agent"]:
            return {"success": False, "error": "agent.name이 필요합니다."}
        
        # 날짜 확인
        agent_time = payload.get("agent", {}).get("time", "")
        if not agent_time:
            return {"success": False, "error": "agent.time이 필요합니다."}
        
        # 반성 처리 시작 시간
        reflection_start_time = time.time()
        reflection_success = await process_reflection_request(payload, client, word2vec_model=word2vec_model)
        reflection_time = time.time() - reflection_start_time
        print(f"⏱ 반성 처리 시간: {reflection_time:.2f}초")
        
        # 계획 처리 시작 시간
        plan_start_time = time.time()
        plan_success, unity_plan = await process_plan_request(payload, client)
        plan_time = time.time() - plan_start_time
        print(f"⏱ 계획 처리 시간: {plan_time:.2f}초")
        
        # 전체 처리 시간 계산
        total_time = time.time() - total_start_time
        print(f"\n⏱ 시간 측정 결과:")
        print(f"  - 반성 처리 시간: {reflection_time:.2f}초")
        print(f"  - 계획 처리 시간: {plan_time:.2f}초")
        print(f"  - 전체 처리 시간: {total_time:.2f}초")
        
        return {
            "success": reflection_success and plan_success,
            "next_day_plan": unity_plan,
            "performance_metrics": {
                "total_time": total_time,
                "reflection_time": reflection_time,
                "plan_time": plan_time
            }
        }
        
    except Exception as e:
        print(f"❌ 반성 및 계획 처리 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}

######################################################################################
###                                     계획                                       ###
######################################################################################

@app.post("/update_embeddings")
async def update_embeddings():
    """
    모든 메모리와 반성의 임베딩을 업데이트하는 엔드포인트
    """
    try:
        print("\n=== 임베딩 업데이트 시작 ===")
        update_counts = embedding_updater.update_embeddings()
        print(f"✅ 임베딩 업데이트 완료: {update_counts}")
        return {
            "success": True,
            "updated": update_counts
        }
    except Exception as e:
        print(f"❌ 임베딩 업데이트 실패: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/conversation")
async def handle_conversation(payload: dict):
    """
    NPC 간 대화를 처리하는 엔드포인트
    
    새 대화 시작, 대화 진행, 대화 종료 및 메모리 저장을 모두 처리합니다.
    최대 대화 턴 수는 NPCConversationManager 내부에서 설정됩니다.
    """
    try:
        # 전체 처리 시작 시간 기록
        start_time = time.time()
        print("\n=== /conversation 엔드포인트 호출 ===")
        print("📥 요청 데이터:", json.dumps(payload, indent=2, ensure_ascii=False))
        
        # 대화 처리
        result = await conversation_manager.process_conversation(payload)
        
        # 처리 시간 계산
        total_time = time.time() - start_time
        print(f"⏱ 대화 처리 시간: {total_time:.2f}초")
        
        # 현재 턴 수 출력
        if result.get("success"):
            current_turns = result.get("turns", 0)
            max_turns = result.get("max_turns", 10)
            print(f"🔄 현재 대화 턴: {current_turns}/{max_turns}")
        
        # 결과에 대화가 종료되었는지 여부 출력
        if result.get("success") and not result.get("should_continue", True):
            print("🔚 대화가 종료되었습니다. 이유:", result.get("conversation", {}).get("end_reason", ""))
            
            # 메모리 ID 출력
            memory_ids = result.get("memory_ids", [])
            if memory_ids:
                print(f"💾 메모리 저장 완료: {memory_ids}")
        
        return result
        
    except Exception as e:
        print(f"❌ 대화 처리 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}
    

def _perform_clear_all_data():
    """
    실제로 모든 데이터 파일을 빈 상태로 초기화하는 내부 함수.
    memories.json, plans.json, reflections.json 파일을 완전히 초기화합니다.
    주의: 이 작업은 되돌릴 수 없습니다.
    """
    try:
        results = {}
        data_dir = os.path.dirname(memory_utils.memories_file)
        
        # 초기화할 파일 목록
        files_to_clear = [
            {"name": "memories", "path": memory_utils.memories_file},
            {"name": "plans", "path": memory_utils.plans_file},
            {"name": "reflections", "path": memory_utils.reflections_file}
        ]
        
        # 각 파일 초기화
        for file_info in files_to_clear:
            file_name = file_info["name"]
            file_path = file_info["path"]
            
            try:
                # 파일 존재 확인
                if not os.path.exists(file_path):
                    print(f"⚠️ {file_name}.json 파일이 존재하지 않습니다. 빈 파일을 생성합니다.")
                    os.makedirs(os.path.dirname(file_path), exist_ok=True)
                
                # 빈 데이터 구조 생성
                empty_data = {}
                
                # 파일에 저장
                with open(file_path, 'w', encoding='utf-8') as f:
                    json.dump(empty_data, f, ensure_ascii=False, indent=2)
                
                print(f"🧹 {file_name}.json 파일이 완전히 초기화되었습니다.")
                
                # 결과 기록
                results[file_name] = {
                    "success": True,
                    "message": f"{file_name}.json file cleared successfully",
                }
                
            except Exception as e:
                print(f"❌ {file_name}.json 초기화 중 오류 발생: {str(e)}")
                results[file_name] = {
                    "success": False,
                    "error": str(e)
                }
        
        # 전체 성공 여부 확인
        overall_success = all(result["success"] for result in results.values())
        
        print(f"데이터 초기화 결과: {'성공' if overall_success else '일부 실패'}")
        return {
            "success": overall_success,
            "message": "All data files have been cleared" if overall_success else "Some files could not be cleared",
            "results": results
        }
        
    except Exception as e:
        print(f"❌ 데이터 초기화 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}

@app.post("/data/clear")
async def clear_all_data():
    """
    모든 데이터 파일을 빈 상태로 초기화하는 엔드포인트
    
    memories.json, plans.json, reflections.json 파일을 완전히 초기화합니다.
    주의: 이 작업은 되돌릴 수 없습니다.
    """
    return _perform_clear_all_data()


@app.post("/data/reset")
async def reset_all_data_from_backup():
    """
    모든 데이터 파일을 각각의 백업 파일로부터 초기화하는 엔드포인트
    
    backup_memories.json, backup_plans.json, backup_reflections.json 파일의 내용으로
    각각 memories.json, plans.json, reflections.json 파일을 초기화합니다.
    """
    try:
        results = {}
        data_dir = os.path.dirname(memory_utils.memories_file)
        
        # 초기화할 파일 목록
        files_to_reset = [
            {"name": "memories", "path": memory_utils.memories_file},
            {"name": "plans", "path": memory_utils.plans_file},
            {"name": "reflections", "path": memory_utils.reflections_file}
        ]
        
        # 각 파일 초기화
        for file_info in files_to_reset:
            file_name = file_info["name"]
            file_path = file_info["path"]
            backup_path = os.path.join(data_dir, f"backup_{file_name}.json")
            
            try:
                # 백업 파일 존재 확인
                if not os.path.exists(backup_path):
                    print(f"⚠️ backup_{file_name}.json 파일이 존재하지 않습니다.")
                    results[file_name] = {
                        "success": False,
                        "error": f"Backup file backup_{file_name}.json not found"
                    }
                    continue
                
                # 백업 파일로부터 초기화
                import shutil
                shutil.copy2(backup_path, file_path)
                
                print(f"🔄 {file_name}.json 파일이 backup_{file_name}.json의 내용으로 초기화되었습니다.")
                
                # 결과 기록
                results[file_name] = {
                    "success": True,
                    "message": f"{file_name}.json reset from backup_{file_name}.json",
                }
                
            except Exception as e:
                print(f"❌ {file_name}.json 초기화 중 오류 발생: {str(e)}")
                results[file_name] = {
                    "success": False,
                    "error": str(e)
                }
        
        # 전체 성공 여부 확인
        overall_success = all(result.get("success", False) for result in results.values())
        
        return {
            "success": overall_success,
            "message": "All data files have been reset from backup" if overall_success else "Some files could not be reset",
            "results": results
        }
        
    except Exception as e:
        print(f"❌ 데이터 초기화 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}


@app.post("/data/save_memories")
async def save_memories_to_backup():
    """
    현재 memories.json 파일의 내용을 backup_memories.json 파일로 복사하여 백업합니다.
    """
    try:
        memories_file_path = memory_utils.memories_file
        if not os.path.exists(memories_file_path):
            print(f"⚠️ 원본 memories.json 파일({memories_file_path})을 찾을 수 없습니다.")
            return {
                "success": False,
                "error": f"memories.json not found at {memories_file_path}"
            }

        data_dir = os.path.dirname(memories_file_path)
        # backup_memories.json 파일명은 reset 기능에서 사용하는 것과 일치해야 함
        backup_file_name = f"backup_{Path(memories_file_path).stem}.json" 
        backup_file_path = os.path.join(data_dir, backup_file_name)

        shutil.copy2(memories_file_path, backup_file_path)
        
        print(f"💾 memories.json 파일이 {backup_file_path}(으)로 성공적으로 백업되었습니다.")
        return {
            "success": True,
            "message": f"memories.json successfully backed up to {backup_file_path}"
        }
        
    except Exception as e:
        print(f"❌ memories.json 백업 중 오류 발생: {str(e)}")
        return {"success": False, "error": str(e)}


if __name__ == "__main__":
    print(f"\n=== 서버 초기화 완료 (총 소요시간: {time.time() - start_time:.2f}초) ===")
    import uvicorn
    _perform_clear_all_data()  # 서버 시작 시 데이터 초기화 함수 호출
    uvicorn.run(app, host="127.0.0.1", port=5000)

