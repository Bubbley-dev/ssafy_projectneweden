# server.py
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
import json
import re
import asyncio
import sys
from pathlib import Path
import os
from datetime import datetime
import time

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
    from agent.modules.retrieve import Retrieve
    print("✅ Retrieve 임포트 완료")
except Exception as e:
    print(f"❌ Retrieve 임포트 실패: {e}")

try:
    import prompts.json_to_prompt as jp
    print("✅ json_to_prompt 임포트 완료")
except Exception as e:
    print(f"❌ json_to_prompt 임포트 실패: {e}")

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

try:
    client = OllamaClient()
    print("✅ OllamaClient 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ OllamaClient 인스턴스 생성 실패: {e}")

try:
    memory_utils = MemoryUtils()
    print("✅ MemoryUtils 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ MemoryUtils 인스턴스 생성 실패: {e}")

try:
    retrieve = Retrieve()
    print("✅ Retrieve 인스턴스 생성 완료")
except Exception as e:
    print(f"❌ Retrieve 인스턴스 생성 실패: {e}")

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

@app.post("/action")
async def receive_data(payload: dict):
    print("Unity로부터 받은 데이터:", payload)

    # 프롬프트 생성
    prompt = jp.format_prompt(payload)
    
    # Future를 사용하여 응답 대기
    future = asyncio.Future()
    
    async def handle_response(response):
        try:
            answer = response.get("response", "")
            
            # 1) 펜스 제거
            cleaned = answer.replace("```json", "").replace("```", "").strip()

            # 2) JSON 텍스트만 추출 (가장 바깥 중괄호 영역)
            match = re.search(r'\{.*\}', cleaned, flags=re.DOTALL)
            if not match:
                future.set_exception(HTTPException(status_code=500, detail="응답에서 JSON을 찾을 수 없습니다."))
                return
            json_text = match.group(0)

            # 3) 파싱
            try:
                action_obj = json.loads(json_text)
                future.set_result(action_obj)
            except json.JSONDecodeError as e:
                future.set_exception(HTTPException(status_code=500, detail=f"JSON 파싱 실패: {e}"))
        except Exception as e:
            future.set_exception(HTTPException(status_code=500, detail=str(e)))

    async def handle_error(error):
        future.set_exception(HTTPException(status_code=500, detail=str(error)))

    # 프롬프트 처리 요청
    await client.process_prompt(
        prompt=prompt,
        system_prompt="You are a helpful AI assistant that responds in JSON format.",
        model_name="gemma3",
        callback=handle_response,
        error_callback=handle_error
    )

    try:
        # 응답 대기
        action_obj = await future
        return {
            "action": action_obj
        }
    except HTTPException as e:
        raise e
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/react")
async def react_to_event(payload: dict):
    try:
        # 요청 데이터 로깅
        print("\n=== /react 엔드포인트 호출 ===")
        print("📥 요청 데이터:", json.dumps(payload, indent=2, ensure_ascii=False))
        
        # 필수 필드 확인
        if not payload or 'event_type' not in payload:
            print("❌ 필수 필드 누락")
            return {"error": "event_type is required"}, 400
            
        # 이벤트 데이터 추출
        event_type = payload.get('event_type')
        agent_name = payload.get('agent_name', 'John')  # 기본값으로 'John' 사용
        
        print(f"🔍 이벤트 타입: {event_type}")
        print(f"👤 에이전트 이름: {agent_name}")
        
        # 이벤트 객체 생성
        event = {
            "type": event_type,
            "agent_name": agent_name
        }
        
        # 이벤트를 문장으로 변환
        event_sentence = memory_utils.event_to_sentence(event)
        print(f"📝 이벤트 문장: {event_sentence}")
        
        # 임베딩 생성
        embedding = memory_utils.get_embedding(event_sentence)
        print(f"🔢 임베딩 생성 완료 (차원: {len(embedding)})")
        
        # 현재 시간 생성
        current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        
        # 프롬프트 생성
        prompt = retrieve.create_reaction_prompt(
            event_sentence=event_sentence,
            event_embedding=embedding,
            agent_name=agent_name,
            prompt_template=load_prompt_file(RETRIEVE_PROMPT_PATH),
            similar_data_cnt=3,  # 유사한 이벤트 3개 포함
            similarity_threshold=0.5  # 유사도 0.5 이상인 이벤트만 포함
        )
        print(f"📋 생성된 프롬프트:\n{prompt}")
        
        # Ollama API 호출
        print("🤖 Ollama API 호출 중...")
        future = asyncio.Future()
        
        async def handle_response(response):
            try:
                answer = response.get("response", "")
                print(f"📥 Ollama 응답: {answer}")
                
                # 1) 펜스 제거
                cleaned = answer.replace("```json", "").replace("```", "").strip()
                print(f"🧹 정제된 응답: {cleaned}")
                
                # 2) JSON 텍스트 추출 (더 유연한 패턴)
                match = re.search(r'\{[\s\S]*\}', cleaned)
                if not match:
                    print("❌ JSON 형식이 아닙니다.")
                    future.set_exception(HTTPException(status_code=500, detail="응답에서 JSON을 찾을 수 없습니다."))
                    return
                json_text = match.group(0)
                print(f"📄 추출된 JSON: {json_text}")

                # 3) 파싱
                try:
                    reaction_obj = json.loads(json_text)
                    print(f"✅ JSON 파싱 성공: {reaction_obj}")
                    
                    # 메모리 저장 (프롬프트 생성 및 API 응답 이후)
                    memory_utils.save_memory(
                        event_sentence=event_sentence,
                        embedding=embedding,
                        event_time=current_time,
                        agent_name=agent_name  # 에이전트 이름 추가
                    )
                    print(f"💾 메모리 저장 완료 (시간: {current_time})")
                    
                    future.set_result(reaction_obj)
                except json.JSONDecodeError as e:
                    print(f"❌ JSON 파싱 실패: {e}")
                    future.set_exception(HTTPException(status_code=500, detail=f"JSON 파싱 실패: {e}"))
            except Exception as e:
                print(f"❌ 응답 처리 중 오류: {e}")
                future.set_exception(HTTPException(status_code=500, detail=str(e)))

        async def handle_error(error):
            future.set_exception(HTTPException(status_code=500, detail=str(error)))

        await client.process_prompt(
            prompt=prompt,
            system_prompt=load_prompt_file(RETRIEVE_SYSTEM_PATH),
            model_name="gemma3",
            callback=handle_response,
            error_callback=handle_error
        )

        # 응답 반환
        reaction = await future
        return {
            "action": reaction
        }
        
    except Exception as e:
        print(f"❌ 오류 발생: {str(e)}")
        return {"error": str(e)}, 500
    
 


try:
    from tests.reflection_pipeline import process_reflection_request
    print("✅ reflection_pipeline 임포트 완료")
except Exception as e:
    print(f"❌ reflection_pipeline 임포트 실패: {e}")

@app.post("/reflect")
async def reflection_endpoint(payload: dict):
    ######################################################################################
    ###                                     반성                                       ###
    ######################################################################################
    """
    반성 엔드포인트
    
    유니티에서 요청하면 당일 또는 지정된 날짜의 메모리에 importance를 추가하고 반성을 생성합니다.
    성공 여부에 따라 true/false를 반환합니다.
    
    요청 형식:
    {
        "agent": {
            "name": "에이전트 이름",
            "time": "YYYY.MM.DD" (선택적)
        }
    }
    """
    try:
        # 요청 데이터 로깅
        print("\n=== /reflect 엔드포인트 호출 ===")
        print("📥 요청 데이터:", json.dumps(payload, indent=2, ensure_ascii=False))
        
        # 필수 필드 확인
        if not payload or 'agent' not in payload or 'name' not in payload['agent']:
            print("❌ 필수 필드 누락")
            raise HTTPException(status_code=400, detail="agent.name is required")
        
        # 날짜 필드 확인 (제거 가능)
        agent_time = payload.get('agent', {}).get('time', '')
        if agent_time:
            print(f"📅 요청된 날짜: {agent_time}")
        else:
            print("📅 날짜가 지정되지 않았습니다. 최신 메모리 날짜를 사용합니다.")

        # 반성 처리 파이프라인 호출
        print("🧠 반성 처리 시작...")
        success = process_reflection_request(payload)
        
        # 결과 로깅
        print(f"✅ 반성 처리 결과: {success}")
        
        # 결과 반환 (success: true/false)
        return {
            "success": success
        }
        
    except Exception as e:
        print(f"❌ 반성 처리 중 오류 발생: {str(e)}")
        raise HTTPException(status_code=500, detail=f"반성 처리 중 오류 발생: {str(e)}")








######################################################################################
###                                     계획                                       ###
######################################################################################






if __name__ == "__main__":
    print(f"\n=== 서버 초기화 완료 (총 소요시간: {time.time() - start_time:.2f}초) ===")
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=5000)
