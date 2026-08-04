import os
import sys

def validate_skills(skills_dir):
    errors = []
    
    if not os.path.exists(skills_dir):
        errors.append(f"Skills directory not found: {skills_dir}")
        return errors
        
    skill_names = set()
    
    for item in os.listdir(skills_dir):
        skill_path = os.path.join(skills_dir, item)
        if os.path.isdir(skill_path):
            skill_md = os.path.join(skill_path, "SKILL.md")
            if not os.path.exists(skill_md):
                errors.append(f"Missing SKILL.md in: {skill_path}")
            else:
                with open(skill_md, "r", encoding="utf-8") as f:
                    content = f.read()
                    
                if not content.startswith("---"):
                    errors.append(f"Missing frontmatter in: {skill_md}")
                else:
                    frontmatter = content.split("---")[1]
                    if "name:" not in frontmatter:
                        errors.append(f"Missing name in frontmatter: {skill_md}")
                    else:
                        name = [line.split("name:")[1].strip() for line in frontmatter.splitlines() if line.startswith("name:")][0]
                        if name in skill_names:
                            errors.append(f"Duplicate skill name: {name}")
                        skill_names.add(name)
                        
                    if "description:" not in frontmatter:
                        errors.append(f"Missing description in frontmatter: {skill_md}")
                        
    return errors

if __name__ == "__main__":
    agents_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), ".agents")
    skills_dir = os.path.join(agents_dir, "skills")
    
    errors = validate_skills(skills_dir)
    if errors:
        print("Validation failed:")
        for error in errors:
            print(f" - {error}")
        sys.exit(1)
    else:
        print("Validation passed. All skills are valid.")
        sys.exit(0)
